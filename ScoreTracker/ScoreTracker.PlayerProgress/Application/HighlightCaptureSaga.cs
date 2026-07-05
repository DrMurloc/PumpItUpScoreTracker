using MassTransit;
using MediatR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Models.Titles.Phoenix;
using ScoreTracker.Domain.Models.Titles.Phoenix2;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.PlayerProgress.Contracts.Events;
using ScoreTracker.PlayerProgress.Contracts.Queries;
using ScoreTracker.PlayerProgress.Domain;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.PlayerProgress.Application;

/// <summary>
///     Write-time noteworthy-score capture (design doc: discord-rich-score-notifications).
///     Computes the highlight flags for every score batch, persists them, and publishes
///     <see cref="ScoreHighlightsCapturedEvent" /> — ALWAYS, even with zero flags or on a
///     capture failure, because the Discord score cards render off that event. The
///     CompetitiveImprover flag is written separately by PlayerRatingSaga (it owns the
///     old-vs-new competitive numbers) and may land after the event publishes; the
///     Sessions page reads the table, so it always shows the complete set.
/// </summary>
internal sealed class HighlightCaptureSaga : IConsumer<PlayerScoresUpdatedEvent>,
    IRequestHandler<GetScoreHighlightsQuery, IEnumerable<ScoreHighlightRecord>>,
    IRequestHandler<GetPlayerMilestonesQuery, IEnumerable<PlayerMilestoneRecord>>
{
    private readonly IMemoryCache _cache;
    private readonly IChartRepository _charts;
    private readonly IScoreHighlightRepository _highlights;
    private readonly ILogger<HighlightCaptureSaga> _logger;
    private readonly IMediator _mediator;
    private readonly IPlayerMilestoneRepository _milestones;
    private readonly IPlayerStatsReader _playerStats;
    private readonly IScoreReader _scores;
    private readonly ITitleRepository _titles;

    public HighlightCaptureSaga(IChartRepository charts, IScoreReader scores, ITitleRepository titles,
        IPlayerStatsReader playerStats, IScoreHighlightRepository highlights,
        IPlayerMilestoneRepository milestones, IMediator mediator, IMemoryCache cache,
        ILogger<HighlightCaptureSaga> logger)
    {
        _charts = charts;
        _scores = scores;
        _titles = titles;
        _playerStats = playerStats;
        _highlights = highlights;
        _milestones = milestones;
        _mediator = mediator;
        _cache = cache;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<PlayerScoresUpdatedEvent> context)
    {
        var e = context.Message;
        var flags = new Dictionary<Guid, HighlightFlag>();
        var writes = new List<ScoreHighlightWrite>();
        var lamps = new List<PlayerMilestoneWrite>();

        if (e.Mix is MixEnum.Phoenix or MixEnum.Phoenix2 && e.Changes.Any())
            try
            {
                writes = await ComputeFlags(e, flags, lamps, context.CancellationToken);
            }
            catch (Exception ex)
            {
                // Capture must never cost the announcement: publish the changes
                // un-flagged and let the page read whatever the table has.
                _logger.LogError(ex, "Highlight capture failed for user {UserId} ({Mix}) — publishing un-flagged",
                    e.UserId, e.Mix);
                flags.Clear();
                writes.Clear();
                lamps.Clear();
            }

        if (writes.Any())
            await _highlights.UpsertFlags(e.Mix, e.UserId, writes, context.CancellationToken);
        if (lamps.Any())
            await _milestones.Append(e.Mix, e.UserId, lamps, context.CancellationToken);

        await context.Publish(ScoreHighlightsCapturedEvent.Create(e.OccurredAt, e.UserId, e.Mix, e.SessionId,
            e.Changes.Select(c => new ScoreHighlightsCapturedEvent.HighlightedChange(c.ChartId, c.IsNewPass,
                c.OldScore, c.NewScore, c.Plate, c.IsBroken,
                flags.TryGetValue(c.ChartId, out var f) ? f : HighlightFlag.None)).ToArray()));
    }

    public async Task<IEnumerable<ScoreHighlightRecord>> Handle(GetScoreHighlightsQuery request,
        CancellationToken cancellationToken)
    {
        return await _highlights.GetHighlights(request.Mix, request.UserId, request.Since, request.Until,
            cancellationToken);
    }

    public async Task<IEnumerable<PlayerMilestoneRecord>> Handle(GetPlayerMilestonesQuery request,
        CancellationToken cancellationToken)
    {
        return await _milestones.GetMilestones(request.Mix, request.UserId, request.Since, request.Until,
            cancellationToken);
    }

    private async Task<List<ScoreHighlightWrite>> ComputeFlags(PlayerScoresUpdatedEvent e,
        Dictionary<Guid, HighlightFlag> flags, List<PlayerMilestoneWrite> lamps,
        CancellationToken cancellationToken)
    {
        var charts = (await _charts.GetCharts(e.Mix, cancellationToken: cancellationToken)).ToDictionary(c => c.Id);
        var bests = (await _scores.GetBestScores(e.Mix, e.UserId, cancellationToken)).ToDictionary(s => s.ChartId);
        var top50 = (await _mediator.Send(new GetTop50ForPlayerQuery(e.UserId, null, Mix: e.Mix), cancellationToken))
            .Select(s => s.ChartId).ToHashSet();
        var completed = (await _titles.GetCompletedTitles(e.Mix, e.UserId, cancellationToken))
            .Select(t => t.Title).ToHashSet();
        var incompleteTitles = (e.Mix == MixEnum.Phoenix
                ? PhoenixTitleList.BuildProgress(charts, bests.Values, completed)
                : Phoenix2TitleList.BuildProgress(charts, bests.Values, completed))
            .OfType<PhoenixTitleProgress>()
            .Where(t => !t.IsComplete && t.Title.CompletionRequired > 0)
            .ToArray();
        var scoringLevels = await _mediator.Send(new GetChartScoringLevelsQuery(e.Mix), cancellationToken);

        // Folder totals and clears come from data already in hand — no extra queries.
        var folderSizes = charts.Values.GroupBy(c => (c.Type, c.Level))
            .ToDictionary(g => g.Key, g => g.Count());
        var folderClears = bests.Values
            .Where(b => !b.IsBroken && b.Score != null && charts.ContainsKey(b.ChartId))
            .GroupBy(b => (charts[b.ChartId].Type, charts[b.ChartId].Level))
            .ToDictionary(g => g.Key, g => g.Count());

        var known = e.Changes
            .Where(c => charts.ContainsKey(c.ChartId) && bests.ContainsKey(c.ChartId))
            .ToArray();

        foreach (var change in known)
        {
            var chart = charts[change.ChartId];
            var best = bests[change.ChartId];
            var f = HighlightFlag.None;
            if (!best.IsBroken && best.Score != null)
            {
                if (top50.Contains(chart.Id)) f |= HighlightFlag.PumbilityTop50;
                if (incompleteTitles.Any(t => t.PhoenixTitle.CompletionProgress(chart, best) > 0))
                    f |= HighlightFlag.TitleProgress;
            }

            if (f != HighlightFlag.None) flags[chart.Id] = f;
        }

        foreach (var folder in known.GroupBy(c => (charts[c.ChartId].Type, charts[c.ChartId].Level)))
        {
            var (type, level) = folder.Key;
            var size = folderSizes.GetValueOrDefault(folder.Key);
            var clears = folderClears.GetValueOrDefault(folder.Key);

            // Score Quality vs comparable players — Singles/Doubles only (competitive
            // cohorts have no Co-Op side).
            if (type is ChartType.Single or ChartType.Double)
            {
                var cohort = await GetCohortScores(e.Mix, e.UserId, type, level, cancellationToken);
                foreach (var change in folder)
                {
                    var best = bests[change.ChartId];
                    if (best.IsBroken || best.Score == null) continue;
                    var cohortScores = cohort.GetValueOrDefault(change.ChartId, Array.Empty<PhoenixScore>());
                    if (ScoreRankings.TieInclusivePercentile(cohortScores, best.Score.Value) >= 0.9)
                        flags[change.ChartId] = flags.GetValueOrDefault(change.ChartId) | HighlightFlag.ScoreQuality90;
                }
            }

            var newPasses = folder.Where(c => c.IsNewPass && !bests[c.ChartId].IsBroken).ToArray();
            CaptureFolderLamps(e, folder.ToArray(), type, level, size, clears, newPasses.Length, charts, bests,
                lamps);
            if (!newPasses.Any()) continue;

            if (size > 0 && clears / (double)size >= 0.9)
                foreach (var pass in newPasses)
                    flags[pass.ChartId] = flags.GetValueOrDefault(pass.ChartId) | HighlightFlag.FolderCompletion90;

            // Folder debut: the first 3 passes ever in this folder (S and D counted
            // separately). A batch landing several at once debuts its top ones by
            // noteworthy ordering.
            var passesBefore = clears - newPasses.Length;
            var debutSlots = 3 - passesBefore;
            if (debutSlots <= 0) continue;
            foreach (var pass in newPasses
                         .OrderByDescending(c => scoringLevels.TryGetValue(c.ChartId, out var sl) ? sl : 0)
                         .ThenByDescending(c => (int?)bests[c.ChartId].Score ?? 0)
                         .Take(debutSlots))
                flags[pass.ChartId] = flags.GetValueOrDefault(pass.ChartId) | HighlightFlag.FolderDebut;
        }

        return known
            .Where(c => flags.GetValueOrDefault(c.ChartId) != HighlightFlag.None)
            .Select(c => new ScoreHighlightWrite(c.ChartId, e.SessionId, e.OccurredAt, flags[c.ChartId],
                charts[c.ChartId].Level,
                scoringLevels.TryGetValue(c.ChartId, out var sl) ? sl : null))
            .ToList();
    }

    // Folder lamps fire on the crossing, every letter and plate boundary, no floor
    // (owner call: lamping is rare and lampers want every gain announced). Under the
    // progress-only journal, a changed chart sitting AT the folder floor implies the
    // floor is newly held — its state had to improve to get there. Grade crossings are
    // verified against the change's old score; old plates aren't on the event, so a
    // plate lamp can rarely re-fire when a floor chart improves score at the same plate.
    private static void CaptureFolderLamps(PlayerScoresUpdatedEvent e,
        PlayerScoresUpdatedEvent.ScoreChange[] folderChanges, ChartType type, DifficultyLevel level, int size,
        int clears, int newPassCount, Dictionary<Guid, Chart> charts,
        Dictionary<Guid, RecordedPhoenixScore> bests, List<PlayerMilestoneWrite> lamps)
    {
        if (size == 0 || clears != size) return;
        var folder = $"{type.GetShortHand()}{(int)level}";
        var newlyCompleted = clears - newPassCount < size;
        if (newlyCompleted)
            lamps.Add(new PlayerMilestoneWrite(MilestoneKind.FolderPassLamp, e.SessionId, e.OccurredAt,
                Detail: folder));

        var folderBests = charts.Values
            .Where(c => c.Type == type && c.Level == level)
            .Select(c => bests.GetValueOrDefault(c.Id))
            .ToArray();
        if (folderBests.Any(b => b?.Score == null || b.IsBroken)) return;

        var minGrade = folderBests.Min(b => b!.Score!.Value.LetterGrade);
        var gradeFloorIsNew = newlyCompleted || folderChanges.Any(c =>
            bests[c.ChartId].Score!.Value.LetterGrade == minGrade
            && (c.IsNewPass || c.OldScore == null ||
                PhoenixScore.From(c.OldScore.Value).LetterGrade < minGrade));
        if (gradeFloorIsNew)
            lamps.Add(new PlayerMilestoneWrite(MilestoneKind.FolderGradeLamp, e.SessionId, e.OccurredAt,
                Detail: $"{folder}|{minGrade.GetName()}"));

        if (folderBests.Any(b => b!.Plate == null)) return;
        var minPlate = folderBests.Min(b => b!.Plate!.Value);
        var plateFloorIsNew = newlyCompleted ||
                              folderChanges.Any(c => bests[c.ChartId].Plate == minPlate);
        if (plateFloorIsNew)
            lamps.Add(new PlayerMilestoneWrite(MilestoneKind.FolderPlateLamp, e.SessionId, e.OccurredAt,
                Detail: $"{folder}|{minPlate}"));
    }

    private async Task<IReadOnlyDictionary<Guid, PhoenixScore[]>> GetCohortScores(MixEnum mix, Guid userId,
        ChartType type, DifficultyLevel level, CancellationToken cancellationToken)
    {
        return await _cache.GetOrCreateAsync(
            $"{nameof(HighlightCaptureSaga)}__Cohort__{mix}__{userId}__{type}__{(int)level}",
            async o =>
            {
                o.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
                var stats = await _playerStats.GetStats(mix, userId, cancellationToken);
                var competitive = type == ChartType.Single
                    ? stats.SinglesCompetitiveLevel
                    : stats.DoublesCompetitiveLevel;
                var players = await _playerStats.GetPlayersByCompetitiveRange(mix, type, competitive, .5,
                    cancellationToken);
                return (IReadOnlyDictionary<Guid, PhoenixScore[]>)(await _scores.GetPlayerScores(mix, players, type,
                        level, cancellationToken))
                    .Where(s => s.record.Score != null)
                    .GroupBy(s => s.record.ChartId)
                    .ToDictionary(g => g.Key,
                        g => g.OrderBy(s => s.record.Score).Select(s => s.record.Score!.Value).ToArray());
            }) ?? new Dictionary<Guid, PhoenixScore[]>();
    }
}
