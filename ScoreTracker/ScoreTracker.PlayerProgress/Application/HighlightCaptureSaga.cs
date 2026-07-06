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
///     The session-snapshot orchestrator (design doc revision 2). The ONLY consumer of
///     the raw score event on the progression side: it computes the highlight flags and
///     folder lamps, then dispatches the rating step and the title step in-process and
///     in order, merges their outputs (rating/title milestones, the CompetitiveImprover
///     flag, per-title progress deltas), and publishes ONE
///     <see cref="ScoreHighlightsCapturedEvent" /> that the Discord card renders from —
///     ALWAYS, even with zero flags: each step is failure-isolated, and a failed step
///     just means its card section is absent. Ordering comes from pipeline shape, not
///     racing consumers (ADR-001 doctrine).
/// </summary>
internal sealed class HighlightCaptureSaga : IConsumer<PlayerScoresUpdatedEvent>,
    IConsumer<UserWeeklyChartsProgressedEvent>,
    IRequestHandler<GetScoreHighlightsQuery, IEnumerable<ScoreHighlightRecord>>,
    IRequestHandler<GetPlayerMilestonesQuery, IEnumerable<PlayerMilestoneRecord>>
{
    private readonly IMemoryCache _cache;
    private readonly IChartRepository _charts;
    private readonly IDateTimeOffsetAccessor _dateTime;
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
        IDateTimeOffsetAccessor dateTime, ILogger<HighlightCaptureSaga> logger)
    {
        _charts = charts;
        _scores = scores;
        _titles = titles;
        _playerStats = playerStats;
        _highlights = highlights;
        _milestones = milestones;
        _mediator = mediator;
        _cache = cache;
        _dateTime = dateTime;
        _logger = logger;
    }

    /// <summary>
    ///     Weekly-board placement changes become milestones (the gold rows on the
    ///     Sessions page). SessionId stays null — weekly registration rides its own
    ///     eligibility flow (import completion / photo submission), not the score
    ///     batches, so there is no batch session to attribute it to.
    /// </summary>
    public async Task Consume(ConsumeContext<UserWeeklyChartsProgressedEvent> context)
    {
        var e = context.Message;
        var chart = (await _charts.GetCharts(e.Mix, chartIds: new[] { e.ChartId },
            cancellationToken: context.CancellationToken)).FirstOrDefault();
        if (chart == null) return;
        await _milestones.Append(e.Mix, e.UserId, new[]
        {
            new PlayerMilestoneWrite(MilestoneKind.WeeklyPlacement, null, _dateTime.Now,
                NewValue: e.Place, Title: chart.Song.Name, Detail: chart.DifficultyString)
        }, context.CancellationToken);
    }

    public async Task Consume(ConsumeContext<PlayerScoresUpdatedEvent> context)
    {
        var e = context.Message;
        var flags = new Dictionary<Guid, HighlightFlags>();
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
                // `writes` was never reassigned on the throwing path — only the
                // by-reference collections the compute mutated need clearing.
                flags.Clear();
                lamps.Clear();
            }

        if (writes.Count > 0)
            await _highlights.UpsertFlags(e.Mix, e.UserId, writes, context.CancellationToken);
        if (lamps.Count > 0)
            await _milestones.Append(e.Mix, e.UserId, lamps, context.CancellationToken);

        var milestones = lamps
            .Select(l => new PlayerMilestoneRecord(l.Kind, l.SessionId, l.OccurredAt, l.OldValue, l.NewValue,
                l.Title, l.Detail))
            .ToList();
        var titleProgress = (IReadOnlyList<TitleProgressDelta>)Array.Empty<TitleProgressDelta>();

        // The rating step: recalc + Pumbility record stats + rating milestones + the
        // CompetitiveImprover flags, which merge into the event so the ⬆ badge rides
        // the card instead of trailing it.
        try
        {
            var stats = await _mediator.Send(new PlayerRatingSaga.CaptureSessionStats(e.UserId, e.Mix,
                e.Changes.Select(c => c.ChartId).Distinct().ToArray(), e.SessionId), context.CancellationToken);
            milestones.AddRange(stats.Milestones);
            foreach (var chartId in stats.ImproverChartIds)
                flags[chartId] = flags.GetValueOrDefault(chartId) | HighlightFlags.CompetitiveImprover;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Rating step failed for user {UserId} ({Mix}) — snapshot ships without stats",
                e.UserId, e.Mix);
        }

        // The title step: completions + paragon gains (announced by the card, not the
        // legacy message) and the per-title progress deltas.
        try
        {
            var titles = await _mediator.Send(new TitleSaga.CaptureSessionTitles(e.UserId, e.Mix, e.SessionId,
                e.Changes), context.CancellationToken);
            milestones.AddRange(titles.Milestones);
            titleProgress = titles.Progress;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Title step failed for user {UserId} ({Mix}) — snapshot ships without titles",
                e.UserId, e.Mix);
        }

        await context.Publish(ScoreHighlightsCapturedEvent.Create(e.OccurredAt, e.UserId, e.Mix, e.SessionId,
            e.Changes.Select(c => new ScoreHighlightsCapturedEvent.HighlightedChange(c.ChartId, c.IsNewPass,
                c.OldScore, c.NewScore, c.Plate, c.IsBroken,
                flags.TryGetValue(c.ChartId, out var f) ? f : HighlightFlags.None)).ToArray(),
            milestones, titleProgress));
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

    /// <summary>Everything the flag computation reads, loaded once per batch.</summary>
    private sealed record CaptureData(
        Dictionary<Guid, Chart> Charts,
        Dictionary<Guid, RecordedPhoenixScore> Bests,
        HashSet<Guid> Top50,
        PhoenixTitleProgress[] IncompleteTitles,
        IDictionary<Guid, double> ScoringLevels,
        Dictionary<(ChartType Type, DifficultyLevel Level), int> FolderSizes,
        Dictionary<(ChartType Type, DifficultyLevel Level), int> FolderClears);

    private async Task<List<ScoreHighlightWrite>> ComputeFlags(PlayerScoresUpdatedEvent e,
        Dictionary<Guid, HighlightFlags> flags, List<PlayerMilestoneWrite> lamps,
        CancellationToken cancellationToken)
    {
        var data = await LoadCaptureData(e, cancellationToken);
        var known = e.Changes
            .Where(c => data.Charts.ContainsKey(c.ChartId) && data.Bests.ContainsKey(c.ChartId))
            .ToArray();

        FlagTop50AndTitleProgress(known, data, flags);
        foreach (var folder in known.GroupBy(c => (data.Charts[c.ChartId].Type, data.Charts[c.ChartId].Level)))
        {
            await FlagScoreQuality(e, folder.Key, folder.ToArray(), data, flags, cancellationToken);
            var newPasses = folder.Where(c => c.IsNewPass && !data.Bests[c.ChartId].IsBroken).ToArray();
            CaptureFolderLamps(e, folder.ToArray(), folder.Key, data, newPasses.Length, lamps);
            FlagFolderCompletionAndDebut(folder.Key, newPasses, data, flags);
        }

        return known
            .Where(c => flags.GetValueOrDefault(c.ChartId) != HighlightFlags.None)
            .Select(c => new ScoreHighlightWrite(c.ChartId, e.SessionId, e.OccurredAt, flags[c.ChartId],
                data.Charts[c.ChartId].Level,
                data.ScoringLevels.TryGetValue(c.ChartId, out var sl) ? sl : null))
            .ToList();
    }

    private async Task<CaptureData> LoadCaptureData(PlayerScoresUpdatedEvent e,
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
        return new CaptureData(charts, bests, top50, incompleteTitles, scoringLevels, folderSizes, folderClears);
    }

    private static void FlagTop50AndTitleProgress(PlayerScoresUpdatedEvent.ScoreChange[] known,
        CaptureData data, Dictionary<Guid, HighlightFlags> flags)
    {
        foreach (var change in known)
        {
            var chart = data.Charts[change.ChartId];
            var best = data.Bests[change.ChartId];
            if (best.IsBroken || best.Score == null) continue;

            var f = HighlightFlags.None;
            if (data.Top50.Contains(chart.Id)) f |= HighlightFlags.PumbilityTop50;
            if (data.IncompleteTitles.Any(t => t.PhoenixTitle.CompletionProgress(chart, best) > 0))
                f |= HighlightFlags.TitleProgress;
            if (f != HighlightFlags.None) flags[chart.Id] = f;
        }
    }

    // Score Quality vs comparable players — Singles/Doubles only (competitive cohorts
    // have no Co-Op side).
    private async Task FlagScoreQuality(PlayerScoresUpdatedEvent e,
        (ChartType Type, DifficultyLevel Level) folder, PlayerScoresUpdatedEvent.ScoreChange[] folderChanges,
        CaptureData data, Dictionary<Guid, HighlightFlags> flags, CancellationToken cancellationToken)
    {
        if (folder.Type is not (ChartType.Single or ChartType.Double)) return;
        var cohort = await GetCohortScores(e.Mix, e.UserId, folder.Type, folder.Level, cancellationToken);
        foreach (var change in folderChanges)
        {
            var best = data.Bests[change.ChartId];
            if (best.IsBroken || best.Score == null) continue;
            var cohortScores = cohort.GetValueOrDefault(change.ChartId, Array.Empty<PhoenixScore>());
            if (ScoreRankings.TieInclusivePercentile(cohortScores, best.Score.Value) >= 0.9)
                flags[change.ChartId] = flags.GetValueOrDefault(change.ChartId) | HighlightFlags.ScoreQuality90;
        }
    }

    private static void FlagFolderCompletionAndDebut((ChartType Type, DifficultyLevel Level) folder,
        PlayerScoresUpdatedEvent.ScoreChange[] newPasses, CaptureData data,
        Dictionary<Guid, HighlightFlags> flags)
    {
        if (newPasses.Length == 0) return;
        var size = data.FolderSizes.GetValueOrDefault(folder);
        var clears = data.FolderClears.GetValueOrDefault(folder);

        if (size > 0 && clears / (double)size >= 0.9)
            foreach (var chartId in newPasses.Select(p => p.ChartId))
                flags[chartId] = flags.GetValueOrDefault(chartId) | HighlightFlags.FolderCompletion90;

        // Folder debut: the first 3 passes ever in this folder (S and D counted
        // separately). A batch landing several at once debuts its top ones by
        // noteworthy ordering.
        var debutSlots = 3 - (clears - newPasses.Length);
        if (debutSlots <= 0) return;
        foreach (var chartId in newPasses
                     .OrderByDescending(c => data.ScoringLevels.TryGetValue(c.ChartId, out var sl) ? sl : 0)
                     .ThenByDescending(c => (int?)data.Bests[c.ChartId].Score ?? 0)
                     .Take(debutSlots)
                     .Select(p => p.ChartId))
            flags[chartId] = flags.GetValueOrDefault(chartId) | HighlightFlags.FolderDebut;
    }

    // Folder lamps fire on the crossing, every letter and plate boundary, no floor
    // (owner call: lamping is rare and lampers want every gain announced). Under the
    // progress-only journal, a changed chart sitting AT the folder floor implies the
    // floor is newly held — its state had to improve to get there. Grade crossings are
    // verified against the change's old score; old plates aren't on the event, so a
    // plate lamp can rarely re-fire when a floor chart improves score at the same plate.
    private static void CaptureFolderLamps(PlayerScoresUpdatedEvent e,
        PlayerScoresUpdatedEvent.ScoreChange[] folderChanges, (ChartType Type, DifficultyLevel Level) folder,
        CaptureData data, int newPassCount, List<PlayerMilestoneWrite> lamps)
    {
        var size = data.FolderSizes.GetValueOrDefault(folder);
        var clears = data.FolderClears.GetValueOrDefault(folder);
        if (size == 0 || clears != size) return;
        var folderName = $"{folder.Type.GetShortHand()}{(int)folder.Level}";
        var newlyCompleted = clears - newPassCount < size;
        if (newlyCompleted)
            lamps.Add(new PlayerMilestoneWrite(MilestoneKind.FolderPassLamp, e.SessionId, e.OccurredAt,
                Detail: folderName));

        var folderBests = data.Charts.Values
            .Where(c => c.Type == folder.Type && c.Level == folder.Level)
            .Select(c => data.Bests.GetValueOrDefault(c.Id))
            .ToArray();
        if (folderBests.Any(b => b?.Score == null || b.IsBroken)) return;

        var minGrade = folderBests.Min(b => b!.Score!.Value.LetterGrade);
        var gradeFloorIsNew = newlyCompleted || folderChanges.Any(c =>
            data.Bests[c.ChartId].Score!.Value.LetterGrade == minGrade
            && (c.IsNewPass || c.OldScore == null ||
                PhoenixScore.From(c.OldScore.Value).LetterGrade < minGrade));
        if (gradeFloorIsNew)
            lamps.Add(new PlayerMilestoneWrite(MilestoneKind.FolderGradeLamp, e.SessionId, e.OccurredAt,
                Detail: $"{folderName}|{minGrade.GetName()}"));

        if (folderBests.Any(b => b!.Plate == null)) return;
        var minPlate = folderBests.Min(b => b!.Plate!.Value);
        var plateFloorIsNew = newlyCompleted ||
                              folderChanges.Any(c => data.Bests[c.ChartId].Plate == minPlate);
        if (plateFloorIsNew)
            lamps.Add(new PlayerMilestoneWrite(MilestoneKind.FolderPlateLamp, e.SessionId, e.OccurredAt,
                Detail: $"{folderName}|{minPlate}"));
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
