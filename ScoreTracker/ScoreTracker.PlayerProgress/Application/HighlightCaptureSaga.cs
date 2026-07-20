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
    IRequestHandler<GetPlayerMilestonesQuery, IEnumerable<PlayerMilestoneRecord>>,
    IRequestHandler<GetScoreHighlightsForSessionsQuery, IEnumerable<ScoreHighlightRecord>>,
    IRequestHandler<GetPlayerMilestonesForSessionsQuery, IEnumerable<PlayerMilestoneRecord>>
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
        var details = new Dictionary<Guid, HighlightDetail>();
        var writes = new List<ScoreHighlightWrite>();
        var lamps = new List<PlayerMilestoneWrite>();

        if (e.Mix is MixEnum.Phoenix or MixEnum.Phoenix2 && e.Changes.Any())
            try
            {
                writes = await ComputeFlags(e, flags, details, lamps, context.CancellationToken);
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
                details.Clear();
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
                flags.TryGetValue(c.ChartId, out var f) ? f : HighlightFlags.None,
                details.GetValueOrDefault(c.ChartId))).ToArray(),
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

    public async Task<IEnumerable<ScoreHighlightRecord>> Handle(GetScoreHighlightsForSessionsQuery request,
        CancellationToken cancellationToken)
    {
        return await _highlights.GetHighlightsBySessions(request.UserId, request.SessionIds, cancellationToken);
    }

    public async Task<IEnumerable<PlayerMilestoneRecord>> Handle(GetPlayerMilestonesForSessionsQuery request,
        CancellationToken cancellationToken)
    {
        return await _milestones.GetMilestonesBySessions(request.UserId, request.SessionIds, cancellationToken);
    }

    /// <summary>Everything the flag computation reads, loaded once per batch.</summary>
    private sealed record CaptureData(
        Dictionary<Guid, Chart> Charts,
        Dictionary<Guid, RecordedPhoenixScore> Bests,
        Dictionary<Guid, int> Top50Ranks,
        PhoenixTitleProgress[] IncompleteTitles,
        IDictionary<Guid, double> ScoringLevels,
        Dictionary<(ChartType Type, DifficultyLevel Level), int> FolderSizes,
        Dictionary<(ChartType Type, DifficultyLevel Level), int> FolderClears,
        double SinglesCompetitive,
        double DoublesCompetitive);

    private async Task<List<ScoreHighlightWrite>> ComputeFlags(PlayerScoresUpdatedEvent e,
        Dictionary<Guid, HighlightFlags> flags, Dictionary<Guid, HighlightDetail> details,
        List<PlayerMilestoneWrite> lamps, CancellationToken cancellationToken)
    {
        var data = await LoadCaptureData(e, cancellationToken);
        var known = e.Changes
            .Where(c => data.Charts.ContainsKey(c.ChartId) && data.Bests.ContainsKey(c.ChartId))
            .ToArray();

        FlagTop50AndTitleProgress(known, data, flags, details);
        foreach (var folder in known.GroupBy(c => (data.Charts[c.ChartId].Type, data.Charts[c.ChartId].Level)))
        {
            await FlagScoreQuality(e, folder.Key, folder.ToArray(), data, flags, details, cancellationToken);
            var newPasses = folder.Where(c => c.IsNewPass && !data.Bests[c.ChartId].IsBroken).ToArray();
            CaptureFolderLamps(e, folder.ToArray(), folder.Key, data, newPasses.Length, lamps);
            FlagFolderCompletionAndDebut(folder.Key, newPasses, data, flags, details);
        }

        return known
            .Where(c => flags.GetValueOrDefault(c.ChartId) != HighlightFlags.None)
            .Select(c => new ScoreHighlightWrite(c.ChartId, e.SessionId, e.OccurredAt, flags[c.ChartId],
                data.Charts[c.ChartId].Level,
                data.ScoringLevels.TryGetValue(c.ChartId, out var sl) ? sl : null,
                details.GetValueOrDefault(c.ChartId)))
            .ToList();
    }

    private async Task<CaptureData> LoadCaptureData(PlayerScoresUpdatedEvent e,
        CancellationToken cancellationToken)
    {
        var charts = (await _charts.GetCharts(e.Mix, cancellationToken: cancellationToken)).ToDictionary(c => c.Id);
        var bests = (await _scores.GetBestScores(e.Mix, e.UserId, cancellationToken)).ToDictionary(s => s.ChartId);
        // Ordered pumbility-desc, so a chart's index is its rank in the player's Pumbility.
        var top50 = (await _mediator.Send(new GetTop50ForPlayerQuery(e.UserId, null, Mix: e.Mix), cancellationToken))
            .Select((s, i) => (s.ChartId, Rank: i + 1))
            .ToDictionary(x => x.ChartId, x => x.Rank);
        var completed = (await _titles.GetCompletedTitles(e.Mix, e.UserId, cancellationToken))
            .Select(t => t.Title).ToHashSet();
        var incompleteTitles = (e.Mix == MixEnum.Phoenix
                ? PhoenixTitleList.BuildProgress(charts, bests.Values, completed)
                : Phoenix2TitleList.BuildProgress(charts, bests.Values, completed))
            .OfType<PhoenixTitleProgress>()
            .Where(t => !t.IsComplete && t.Title.CompletionRequired > 0)
            .ToArray();
        var scoringLevels = await _mediator.Send(new GetChartScoringLevelsQuery(e.Mix), cancellationToken);

        // Competitive levels gate Score Quality (and are cheap to carry): a back-filled chart
        // more than 5 levels under the player's competitive level for its type is noise, not a
        // peer flag — the cohort is never even built for it.
        var stats = await _playerStats.GetStats(e.Mix, e.UserId, cancellationToken);

        // Folder totals and clears come from data already in hand — no extra queries.
        var folderSizes = charts.Values.GroupBy(c => (c.Type, c.Level))
            .ToDictionary(g => g.Key, g => g.Count());
        var folderClears = bests.Values
            .Where(b => !b.IsBroken && b.Score != null && charts.ContainsKey(b.ChartId))
            .GroupBy(b => (charts[b.ChartId].Type, charts[b.ChartId].Level))
            .ToDictionary(g => g.Key, g => g.Count());
        return new CaptureData(charts, bests, top50, incompleteTitles, scoringLevels, folderSizes, folderClears,
            stats.SinglesCompetitiveLevel, stats.DoublesCompetitiveLevel);
    }

    private const int PerfectGameScore = 1_000_000;

    // Accumulates per-chart caption detail across the flag passes. Records are immutable,
    // so each pass reads the current value and `with`-updates the field it owns.
    private static HighlightDetail Detail(Dictionary<Guid, HighlightDetail> details, Guid id)
    {
        return details.TryGetValue(id, out var d) ? d : new HighlightDetail();
    }

    private static void FlagTop50AndTitleProgress(PlayerScoresUpdatedEvent.ScoreChange[] known,
        CaptureData data, Dictionary<Guid, HighlightFlags> flags, Dictionary<Guid, HighlightDetail> details)
    {
        foreach (var change in known)
        {
            var chart = data.Charts[change.ChartId];
            var best = data.Bests[change.ChartId];
            if (best.IsBroken || best.Score == null) continue;

            var f = HighlightFlags.None;
            if (data.Top50Ranks.TryGetValue(chart.Id, out var rank))
            {
                f |= HighlightFlags.PumbilityTop50;
                details[chart.Id] = Detail(details, chart.Id) with { PumbilityRank = rank };
            }

            // Per-row title progress is chart-specific only (skill titles). Generic
            // difficulty/co-op progress rides the card's top section as % deltas, not a row
            // caption — so a level's worth of upscores no longer each claim a title flag.
            var skill = data.IncompleteTitles
                .Select(t => t.PhoenixTitle)
                .OfType<PhoenixSkillTitle>()
                .FirstOrDefault(t => t.AppliesToChart(chart) && t.CompletionProgress(chart, best) > 0);
            if (skill != null)
            {
                f |= HighlightFlags.TitleProgress;
                details[chart.Id] = Detail(details, chart.Id) with
                {
                    SkillTitleName = skill.Name.ToString(),
                    SkillTitleScore = (int)best.Score.Value,
                    SkillTitleThreshold = skill.CompletionRequired
                };
            }

            if (f != HighlightFlags.None) flags[chart.Id] = flags.GetValueOrDefault(chart.Id) | f;
        }
    }

    // Score Quality vs comparable players — Singles/Doubles only (competitive cohorts
    // have no Co-Op side).
    private async Task FlagScoreQuality(PlayerScoresUpdatedEvent e,
        (ChartType Type, DifficultyLevel Level) folder, PlayerScoresUpdatedEvent.ScoreChange[] folderChanges,
        CaptureData data, Dictionary<Guid, HighlightFlags> flags, Dictionary<Guid, HighlightDetail> details,
        CancellationToken cancellationToken)
    {
        if (folder.Type is not (ChartType.Single or ChartType.Double)) return;

        // Owner call: below (competitive − 5) for the chart's type, peer comparison is noise
        // (a 23-competitive player back-filling S5s). Skip the whole folder — no cohort, no flag.
        var competitive = folder.Type == ChartType.Single ? data.SinglesCompetitive : data.DoublesCompetitive;
        if ((int)folder.Level < competitive - 5) return;

        var cohort = await GetCohortScores(e.Mix, e.UserId, folder.Type, folder.Level, competitive,
            cancellationToken);
        foreach (var change in folderChanges)
        {
            var best = data.Bests[change.ChartId];
            if (best.IsBroken || best.Score == null) continue;
            var cohortScores = cohort.GetValueOrDefault(change.ChartId, Array.Empty<PhoenixScore>());
            // "Top scores among peers" needs peers.
            if (cohortScores.Length == 0) continue;
            if (ScoreRankings.TieInclusivePercentile(cohortScores, best.Score.Value) < 0.9) continue;

            var score = (int)best.Score.Value;
            var pgCount = cohortScores.Count(s => (int)s == PerfectGameScore);
            // A PG most peers also hold isn't noteworthy (owner call) — suppress it.
            if (score == PerfectGameScore && pgCount * 2 > cohortScores.Length) continue;

            flags[change.ChartId] = flags.GetValueOrDefault(change.ChartId) | HighlightFlags.ScoreQuality90;
            details[change.ChartId] = Detail(details, change.ChartId) with
            {
                PeerCount = cohortScores.Length,
                PeerBetterCount = cohortScores.Count(s => (int)s > score),
                PeerPgCount = pgCount
            };
        }
    }

    private static void FlagFolderCompletionAndDebut((ChartType Type, DifficultyLevel Level) folder,
        PlayerScoresUpdatedEvent.ScoreChange[] newPasses, CaptureData data,
        Dictionary<Guid, HighlightFlags> flags, Dictionary<Guid, HighlightDetail> details)
    {
        if (newPasses.Length == 0) return;
        var size = data.FolderSizes.GetValueOrDefault(folder);
        var clears = data.FolderClears.GetValueOrDefault(folder);

        if (size > 0 && clears / (double)size >= 0.9)
            foreach (var chartId in newPasses.Select(p => p.ChartId))
                flags[chartId] = flags.GetValueOrDefault(chartId) | HighlightFlags.FolderCompletion90;

        // Folder debut: the first 3 passes ever in this folder (S and D counted
        // separately). A batch landing several at once debuts its top ones by noteworthy
        // ordering; the ordinal (First/Second/Third) is the prior clear count plus place.
        var priorClears = clears - newPasses.Length;
        var debutSlots = 3 - priorClears;
        if (debutSlots <= 0) return;
        var ordinal = priorClears;
        foreach (var chartId in newPasses
                     .OrderByDescending(c => data.ScoringLevels.TryGetValue(c.ChartId, out var sl) ? sl : 0)
                     .ThenByDescending(c => (int?)data.Bests[c.ChartId].Score ?? 0)
                     .Take(debutSlots)
                     .Select(p => p.ChartId))
        {
            ordinal++;
            flags[chartId] = flags.GetValueOrDefault(chartId) | HighlightFlags.FolderDebut;
            details[chartId] = Detail(details, chartId) with { FolderDebutOrdinal = ordinal };
        }
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

        var minGrade = folderBests.Min(b => b!.Score!.Value.LetterGradeFor(e.Mix));
        var gradeFloorIsNew = newlyCompleted || folderChanges.Any(c =>
            data.Bests[c.ChartId].Score!.Value.LetterGradeFor(e.Mix) == minGrade
            && (c.IsNewPass || c.OldScore == null ||
                PhoenixScore.From(c.OldScore.Value).LetterGradeFor(e.Mix) < minGrade));
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
        ChartType type, DifficultyLevel level, double competitive, CancellationToken cancellationToken)
    {
        return await _cache.GetOrCreateAsync(
            $"{nameof(HighlightCaptureSaga)}__Cohort__{mix}__{userId}__{type}__{(int)level}",
            async o =>
            {
                o.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
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
