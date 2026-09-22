using MassTransit;
using MediatR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.PlayerProgress.Contracts.Events;
using ScoreTracker.PlayerProgress.Contracts.Queries;
using ScoreTracker.PlayerProgress.Domain;
using ScoreTracker.SharedKernel.Caching;
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
    private readonly IPlayerFolderLevelRepository _folderLevels;
    private readonly IScoreHighlightRepository _highlights;
    private readonly ILogger<HighlightCaptureSaga> _logger;
    private readonly IMediator _mediator;
    private readonly IPlayerMilestoneRepository _milestones;
    private readonly IPlayerStatsReader _playerStats;
    private readonly IScoreReader _scores;
    private readonly IScoreAttemptReader _attempts;
    private readonly IOfficialPlacementReader _officialPlacements;
    private readonly ISeasonReader _seasons;

    public HighlightCaptureSaga(IChartRepository charts, IScoreReader scores,
        IPlayerStatsReader playerStats, IScoreHighlightRepository highlights,
        IPlayerMilestoneRepository milestones, IPlayerFolderLevelRepository folderLevels, IMediator mediator,
        IMemoryCache cache, IDateTimeOffsetAccessor dateTime, IScoreAttemptReader attempts,
        IOfficialPlacementReader officialPlacements, ISeasonReader seasons,
        ILogger<HighlightCaptureSaga> logger)
    {
        _seasons = seasons;
        _attempts = attempts;
        _officialPlacements = officialPlacements;
        _charts = charts;
        _scores = scores;
        _playerStats = playerStats;
        _highlights = highlights;
        _milestones = milestones;
        _folderLevels = folderLevels;
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

        // Hoisted out of ComputeFlags: the Hardmode step below writes highlight rows too, and a
        // row it has to CREATE needs the chart's level. Loading it twice would be two reads of
        // the same charts in one batch.
        CaptureData? captured = null;

        if (e.Mix is MixEnum.Phoenix or MixEnum.Phoenix2 && e.Changes.Any())
            try
            {
                captured = await LoadCaptureData(e, context.CancellationToken);
                writes = await ComputeFlags(e, captured, flags, details, lamps, context.CancellationToken);
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
                captured = null;
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
        // Declared out here so a failed Hardmode step leaves it at None and every reader below
        // sees "Hardmode is not live here" rather than a missing variable.
        var hardmode = HardmodeSaga.HardmodeReprice.None;

        // The rating step: recalc + Pumbility record stats + rating milestones + the
        // CompetitiveImprover flags, which merge into the event so the ⬆ badge rides
        // the card instead of trailing it. A mix without a PUMBILITY formula has no stats
        // to capture, and asking would throw — skipped rather than logged as a failure
        // on every session.
        if (e.Mix.HasPumbility())
            try
            {
                var stats = await _mediator.Send(new PlayerRatingSaga.CaptureSessionStats(e.UserId, e.Mix,
                        e.Changes.Select(c => c.ChartId).Distinct().ToArray(), e.SessionId, e.Changes),
                    context.CancellationToken);
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

        // The Hardmode step: this account's standing on the Hardmode board. The qualifying
        // chart list is rebuilt weekly so the board does not move under players day to day,
        // but a standing ON it moves with the import, like every other PUMBILITY number
        // (owner, 2026-09-12). Failure-isolated for the same reason as the two above: a
        // second board's total is not worth losing the session card over.
        try
        {
            hardmode = await _mediator.Send(new HardmodeSaga.RepriceHardmodePool(e.UserId, e.Mix, e.Changes),
                context.CancellationToken);

            // Hardmode announces on the surfaces PUMBILITY announces on (D18), so its movement
            // mints the same shape of milestone and its scores wear the same shape of mark. Both
            // happen HERE rather than in the step, because a milestone is a capture fact: the
            // step's job is the board, and the sweep calls it too.
            var hardmodeLamps = HardmodeMilestones(hardmode, e.SessionId);
            if (hardmodeLamps.Count > 0)
            {
                await _milestones.Append(e.Mix, e.UserId, hardmodeLamps, context.CancellationToken);
                milestones.AddRange(hardmodeLamps.Select(l => new PlayerMilestoneRecord(l.Kind, l.SessionId,
                    l.OccurredAt, l.OldValue, l.NewValue, l.Title, l.Detail)));
            }

            // Mutates the same dictionaries the event is built from below, so the mark reaches
            // the Discord card and the feeds in the same publish rather than trailing it.
            var hardmodeWrites = FlagHardmode(e, hardmode, captured, flags, details);
            if (hardmodeWrites.Count > 0)
                await _highlights.UpsertFlags(e.Mix, e.UserId, hardmodeWrites, context.CancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Hardmode step failed for user {UserId} ({Mix}) — the board keeps its last total",
                e.UserId, e.Mix);
        }

        // The season step: the same rating arithmetic a second time over the seasonal pool, into
        // the season's stats row (docs/design/seasons.md §4.2). Quiet by construction — no seasonal
        // milestone, lamp or title in v1 (D18) — and failure-isolated like the three above, because
        // a second pool's total is not worth losing the session card over. Its own folder levels
        // ride the same pass, further down where the all-time ones are written.
        try
        {
            // The running season, which is the only one an import can have written to (D13).
            if (await RunningSeason(e.Mix, context.CancellationToken) is { } season)
                await _mediator.Send(new PlayerRatingSaga.CaptureSessionStats(e.UserId, e.Mix,
                        e.Changes.Select(c => c.ChartId).Distinct().ToArray(), e.SessionId, e.Changes, season),
                    context.CancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Season pass failed for user {UserId} ({Mix}) — the season row keeps its last total",
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
        IDictionary<Guid, double> ScoringLevels,
        Dictionary<(ChartType Type, DifficultyLevel Level), int> FolderSizes,
        Dictionary<(ChartType Type, DifficultyLevel Level), int> FolderClears,
        PlayerStatsRecord Stats);

    private async Task<List<ScoreHighlightWrite>> ComputeFlags(PlayerScoresUpdatedEvent e, CaptureData data,
        Dictionary<Guid, HighlightFlags> flags, Dictionary<Guid, HighlightDetail> details,
        List<PlayerMilestoneWrite> lamps, CancellationToken cancellationToken)
    {
        var known = e.Changes
            .Where(c => data.Charts.ContainsKey(c.ChartId) && data.Bests.ContainsKey(c.ChartId))
            .ToArray();

        FlagTop50(known, data, flags, details);

        // The folder standings this batch moved. Built from data already in hand, one record per
        // touched folder rather than per chart, and saved in a single call below. The stored rows
        // are the only record of where the player stood before this batch, so they are also what
        // a movement gets diffed against — and a folder with no stored row seeds silently.
        var passed = FolderLevelCalculator.PassedScores(data.Bests.Values);
        var previousFolders = await LoadPreviousFolderLevels(e, cancellationToken);
        var touchedFolders = new List<FolderLevelRecord>();

        foreach (var folder in known.GroupBy(c => (data.Charts[c.ChartId].Type, data.Charts[c.ChartId].Level)))
        {
            await FlagScoreQuality(e, folder.Key, folder.ToArray(), data, flags, details, cancellationToken);
            var newPasses = folder.Where(c => c.IsNewPass && !data.Bests[c.ChartId].IsBroken).ToArray();
            CaptureFolderLamps(e, folder.ToArray(), folder.Key, data, newPasses.Length, lamps);
            FlagFolderCompletionAndDebut(folder.Key, newPasses, data, flags, details);

            var level = FolderLevelCalculator.ComputeOne(e.Mix, folder.Key.Type, folder.Key.Level,
                data.Charts.Values, passed);
            if (level == null) continue;
            touchedFolders.Add(level);

            var moved = FolderLevelCalculator.Diff(previousFolders?.GetValueOrDefault(level.Folder), level,
                e.SessionId, e.OccurredAt);
            if (moved != null) lamps.Add(moved);
        }

        await SaveFolderLevels(e, touchedFolders, cancellationToken);
        await SaveSeasonFolderLevels(e, touchedFolders, data, cancellationToken);

        await FlagOfficialPlacements(e, known, data, flags, details, cancellationToken);
        await RecordAttempts(e, known, data, details, cancellationToken);

        // A row is written when the batch learned ANYTHING about the score — a flag, or just
        // its standing among peers. Detail-only rows are what let the page colour an ordinary
        // score; charts with neither (co-op, or below the competitive gate) stay unwritten,
        // and the page renders those in plain ink.
        return known
            .Where(c => flags.GetValueOrDefault(c.ChartId) != HighlightFlags.None
                        || details.ContainsKey(c.ChartId))
            .Select(c => new ScoreHighlightWrite(c.ChartId, e.SessionId, e.OccurredAt,
                flags.GetValueOrDefault(c.ChartId),
                data.Charts[c.ChartId].Level,
                data.ScoringLevels.TryGetValue(c.ChartId, out var sl) ? sl : null,
                details.GetValueOrDefault(c.ChartId)))
            .ToList();
    }

    /// <summary>
    ///     Flags scores that place inside their chart's mirrored official board. Estimated
    ///     against the last sealed snapshot, so the detail carries the board's date and the UI
    ///     prints a "~" — the sweep runs weekly and has not seen tonight's scores.
    /// </summary>
    private async Task FlagOfficialPlacements(PlayerScoresUpdatedEvent e,
        PlayerScoresUpdatedEvent.ScoreChange[] known, CaptureData data,
        Dictionary<Guid, HighlightFlags> flags, Dictionary<Guid, HighlightDetail> details,
        CancellationToken cancellationToken)
    {
        var scored = known
            .Select(c => data.Bests[c.ChartId])
            .Where(b => !b.IsBroken && b.Score != null)
            .Select(b => (b.ChartId, Score: (int)b.Score!.Value))
            .ToArray();
        if (scored.Length == 0) return;

        try
        {
            var placements = await _officialPlacements.EstimatePlacements(e.Mix, e.UserId, scored,
                cancellationToken);
            foreach (var (chartId, estimate) in placements)
            {
                flags[chartId] = flags.GetValueOrDefault(chartId) | HighlightFlags.OfficialBoardPlacement;
                details[chartId] = Detail(details, chartId) with
                {
                    OfficialPlace = estimate.Place,
                    OfficialBoardDepth = estimate.BoardDepth,
                    OfficialAsOf = estimate.AsOf
                };
            }
        }
        catch (Exception ex)
        {
            // The mirror is a different vertical on a weekly cadence — a bad snapshot read
            // costs this caption, never the capture around it.
            _logger.LogError(ex, "Official placement estimate failed for user {UserId} ({Mix})", e.UserId, e.Mix);
        }
    }

    /// <summary>
    ///     Records how many times a chart was played before the play that cleared it. New passes
    ///     only, and only within this session — the journal has held losing plays since
    ///     2026-07-30 and only as deep as the site's recently-played page reached.
    /// </summary>
    private async Task RecordAttempts(PlayerScoresUpdatedEvent e, PlayerScoresUpdatedEvent.ScoreChange[] known,
        CaptureData data, Dictionary<Guid, HighlightDetail> details, CancellationToken cancellationToken)
    {
        if (e.SessionId == null) return;
        var passes = known
            .Where(c => c.IsNewPass && !data.Bests[c.ChartId].IsBroken)
            .Select(c => c.ChartId)
            .ToArray();
        if (passes.Length == 0) return;

        try
        {
            var counts = await _attempts.GetSessionAttemptCounts(e.UserId, e.SessionId.Value, passes,
                cancellationToken);
            foreach (var (chartId, attempts) in counts)
                details[chartId] = Detail(details, chartId) with { AttemptsBeforeClear = attempts };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Attempt counts failed for user {UserId} ({Mix})", e.UserId, e.Mix);
        }
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
        var scoringLevels = await _mediator.Send(new GetChartScoringLevelsQuery(e.Mix), cancellationToken);

        // Competitive levels gate two flags: a chart more than 5 levels under the player's
        // competitive level for its type never gets a peer cohort built at all, and a folder
        // below that level debuts nothing.
        var stats = await _playerStats.GetStats(e.Mix, e.UserId, cancellationToken);

        // Folder totals and clears come from data already in hand — no extra queries.
        var folderSizes = charts.Values.GroupBy(c => (c.Type, c.Level))
            .ToDictionary(g => g.Key, g => g.Count());
        var folderClears = bests.Values
            .Where(b => !b.IsBroken && b.Score != null && charts.ContainsKey(b.ChartId))
            .GroupBy(b => (charts[b.ChartId].Type, charts[b.ChartId].Level))
            .ToDictionary(g => g.Key, g => g.Count());
        return new CaptureData(charts, bests, top50, scoringLevels, folderSizes, folderClears, stats);
    }

    private const int PerfectGameScore = 1_000_000;

    // Accumulates per-chart caption detail across the flag passes. Records are immutable,
    // so each pass reads the current value and `with`-updates the field it owns.
    private static HighlightDetail Detail(Dictionary<Guid, HighlightDetail> details, Guid id)
    {
        return details.TryGetValue(id, out var d) ? d : new HighlightDetail();
    }

    /// <summary>
    ///     Hardmode's three pool milestones, mirroring PUMBILITY's three exactly (D20) — it is
    ///     the same progression shape, so it gets the same strip, the same Discord line and the
    ///     same history tag. Only pools that actually moved mint one: a session that touched no
    ///     qualifying chart announces nothing, which is most sessions.
    /// </summary>
    private List<PlayerMilestoneWrite> HardmodeMilestones(HardmodeSaga.HardmodeReprice hardmode, Guid? sessionId)
    {
        var lamps = new List<PlayerMilestoneWrite>();
        // Nothing to announce if nothing was stored: an account with no PlayerStats row is
        // skipped by the write, and a milestone against an unpersisted number repeats itself on
        // every subsequent import. Normally unreachable - the rating step creates the row first -
        // but that step is failure-isolated and this one runs regardless.
        if (!hardmode.Persisted) return lamps;
        Add(MilestoneKind.HardmodePumbilityGain, hardmode.Combined);
        Add(MilestoneKind.HardmodeSinglesPumbilityGain, hardmode.Singles);
        Add(MilestoneKind.HardmodeDoublesPumbilityGain, hardmode.Doubles);
        return lamps;

        void Add(MilestoneKind kind, HardmodeSaga.HardmodePoolMove move)
        {
            if (!move.Gained) return;
            // Detail carries the standing as "place|field", the way TitleProgress carries
            // "S21|3120|4000" — the feeds need a rank to decide whether a climb is a win, and a
            // milestone is what reaches them. Absent when the pool holds nothing rankable.
            var standing = move.Rank is { } place && move.Field is { } field ? $"{place}|{field}" : null;
            lamps.Add(new PlayerMilestoneWrite(kind, sessionId, _dateTime.Now, move.Old, move.New,
                Detail: standing));
        }
    }

    /// <summary>
    ///     The skull beside the crown: every changed score that sits in the viewer's Hardmode
    ///     fifty, carrying its slot and what tonight's play added to that pool (D21).
    ///     <para>
    ///         Deliberately the literal twin of <see cref="FlagTop50" /> rather than "this is a
    ///         qualifying chart" — a pool under fifty therefore lights up nearly everything it
    ///         holds, which is the same bootstrap the normal top 50 had and tightens on its own.
    ///     </para>
    ///     <para>
    ///         Returns writes rather than mutating alone, because these land after the first
    ///         upsert; <c>UpsertFlags</c> ORs into the row that already exists and creates one
    ///         where it does not, which is why the chart's level has to be to hand.
    ///     </para>
    /// </summary>
    private static List<ScoreHighlightWrite> FlagHardmode(PlayerScoresUpdatedEvent e,
        HardmodeSaga.HardmodeReprice hardmode, CaptureData? data,
        Dictionary<Guid, HighlightFlags> flags, Dictionary<Guid, HighlightDetail> details)
    {
        var writes = new List<ScoreHighlightWrite>();
        if (data == null || hardmode.Ranks.Count == 0) return writes;

        foreach (var change in e.Changes)
        {
            if (!hardmode.Ranks.TryGetValue(change.ChartId, out var rank)) continue;
            if (!data.Charts.TryGetValue(change.ChartId, out var chart)) continue;
            // A broken run prices to zero and holds no slot, so it cannot be in the fifty at
            // all — but the best on record is what the pool was priced from, not this change.
            if (!data.Bests.TryGetValue(change.ChartId, out var best) || best.IsBroken || best.Score == null)
                continue;

            var gain = hardmode.Gains.TryGetValue(change.ChartId, out var g) && g > 0 ? g : (double?)null;
            flags[change.ChartId] = flags.GetValueOrDefault(change.ChartId) | HighlightFlags.HardmodeTop50;
            details[change.ChartId] = (details.GetValueOrDefault(change.ChartId) ?? new HighlightDetail())
                with { HardmodeRank = rank, HardmodeGain = gain };

            writes.Add(new ScoreHighlightWrite(change.ChartId, e.SessionId, e.OccurredAt,
                HighlightFlags.HardmodeTop50, chart.Level,
                data.ScoringLevels.TryGetValue(change.ChartId, out var sl) ? sl : null,
                details[change.ChartId]));
        }

        return writes;
    }

    private static void FlagTop50(PlayerScoresUpdatedEvent.ScoreChange[] known,
        CaptureData data, Dictionary<Guid, HighlightFlags> flags, Dictionary<Guid, HighlightDetail> details)
    {
        foreach (var change in known)
        {
            var chart = data.Charts[change.ChartId];
            var best = data.Bests[change.ChartId];
            if (best.IsBroken || best.Score == null) continue;

            if (!data.Top50Ranks.TryGetValue(chart.Id, out var rank)) continue;
            flags[chart.Id] = flags.GetValueOrDefault(chart.Id) | HighlightFlags.PumbilityTop50;
            details[chart.Id] = Detail(details, chart.Id) with { PumbilityRank = rank };
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
        var competitive = folder.Type == ChartType.Single
            ? data.Stats.SinglesCompetitiveLevel
            : data.Stats.DoublesCompetitiveLevel;
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

            var score = (int)best.Score.Value;
            var percentile = ScoreRankings.TieInclusivePercentile(cohortScores, best.Score.Value);
            var pgCount = cohortScores.Count(s => (int)s == PerfectGameScore);

            // The cohort standing is recorded for EVERY score it could be computed for, not
            // only the ones that clear the flag — the Sessions page colours every row by this
            // percentile, and a row with no number would read as a bad score rather than an
            // unmeasured one. The flag below keeps its own, stricter bar.
            details[change.ChartId] = Detail(details, change.ChartId) with
            {
                PeerCount = cohortScores.Length,
                PeerBetterCount = cohortScores.Count(s => (int)s > score),
                PeerPgCount = pgCount,
                PeerPercentile = percentile
            };

            if (percentile < 0.9) continue;
            // A PG most peers also hold isn't noteworthy (owner call) — suppress it.
            if (score == PerfectGameScore && pgCount * 2 > cohortScores.Length) continue;

            flags[change.ChartId] = flags.GetValueOrDefault(change.ChartId) | HighlightFlags.ScoreQuality90;
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
        // separately), at or above the player's floored competitive level for the folder's
        // discipline. Below that line a first pass is a back-fill of ground already held, not a
        // debut — and it is the case that fires hardest, because the folders a player has never
        // touched are the easy ones. Same bar the community and rival feeds apply to the same
        // event, so a row cannot be marked here and withheld there.
        //
        // CO-OP NEVER DEBUTS, by design: its "level" is a player count of 2–5 and the bar it
        // reads is the overall competitive level, so the gate always closes. See
        // CompetitiveLevels.Floor — a co-op folder names a party size, not a rung.
        //
        // A batch landing several at once debuts its top ones by noteworthy ordering; the ordinal
        // (First/Second/Third) is the prior clear count plus place.
        if ((int)folder.Level < CompetitiveLevels.Floor(folder.Type, data.Stats)) return;
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

    /// <summary>
    ///     Where the player stood before this batch, keyed by folder. Null — not empty — when the
    ///     read fails, so a lookup miss stays honest: an empty dictionary would read as "every
    ///     folder is new" and silence a whole session's worth of real movements.
    /// </summary>
    private async Task<Dictionary<string, FolderLevelRecord>?> LoadPreviousFolderLevels(
        PlayerScoresUpdatedEvent e, CancellationToken cancellationToken)
    {
        try
        {
            return (await _folderLevels.GetFolderLevels(e.Mix, e.UserId, cancellationToken))
                .ToDictionary(l => l.Folder);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Folder level read failed for user {UserId} ({Mix}) — movements not announced",
                e.UserId, e.Mix);
            return null;
        }
    }

    /// <summary>
    ///     Persists the standings for the folders this batch touched
    ///     (docs/design/folder-level-progression.md §4). Isolated from the rest of the capture:
    ///     ComputeFlags already runs inside one try/catch, so an unguarded write here would cost
    ///     the flags and the lamps too — a projection failure must only lose the projection.
    /// </summary>
    private async Task SaveFolderLevels(PlayerScoresUpdatedEvent e, IReadOnlyCollection<FolderLevelRecord> levels,
        CancellationToken cancellationToken)
    {
        if (levels.Count == 0) return;
        try
        {
            await _folderLevels.Save(e.UserId, levels, e.OccurredAt, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Folder level write failed for user {UserId} ({Mix}) — {Count} folders skipped",
                e.UserId, e.Mix, levels.Count);
        }

    }

    /// <summary>
    ///     The one season an import can have written to (docs/design/seasons.md §1, D13): none at all
    ///     unless the mix has seasons — Phoenix 1 never does — and otherwise the season the clock
    ///     stands in, since there is no grace and a closed season takes no further plays.
    /// </summary>
    private async Task<SeasonId?> RunningSeason(MixEnum mix, CancellationToken cancellationToken)
    {
        if (!mix.HasSeasons()) return null;
        var running = await _seasons.GetSeasonAt(_dateTime.Now, cancellationToken);
        return running is { IsSealed: false } ? running.Id : null;
    }

    /// <summary>
    ///     The same folders again over the seasonal pool (docs/design/seasons.md §4.2, D38): the
    ///     identical calculator, given the season's bests instead of the all-time ones, so a broken
    ///     in-window play counts as played exactly as it does all-time — this is one rule run twice,
    ///     not two rules. Folders are chosen by the batch, as above; only the scores filling them
    ///     differ. Failure-isolated for the same reason: a second set of folders is not worth losing
    ///     the session card over.
    /// </summary>
    private async Task SaveSeasonFolderLevels(PlayerScoresUpdatedEvent e,
        IReadOnlyCollection<FolderLevelRecord> touched, CaptureData data, CancellationToken cancellationToken)
    {
        if (touched.Count == 0) return;
        try
        {
            if (await RunningSeason(e.Mix, cancellationToken) is { } season)
            {
                var seasonBests = await _scores.GetBestScores(e.Mix, e.UserId, season, cancellationToken);
                var passed = FolderLevelCalculator.PassedScores(seasonBests);
                var levels = touched
                    .Select(f => FolderLevelCalculator.ComputeOne(e.Mix, f.Type, f.Level, data.Charts.Values, passed))
                    .Where(l => l != null)
                    .Select(l => l!)
                    .ToArray();
                if (levels.Length == 0) return;

                await _folderLevels.Save(e.UserId, levels, e.OccurredAt, season, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Season folder write failed for user {UserId} ({Mix}) — {Count} folders skipped",
                e.UserId, e.Mix, touched.Count);
        }
    }

    private async Task<IReadOnlyDictionary<Guid, PhoenixScore[]>> GetCohortScores(MixEnum mix, Guid userId,
        ChartType type, DifficultyLevel level, double competitive, CancellationToken cancellationToken)
    {
        // A Viewer key: the entry is one player's neighborhood — the user id stands in for their
        // competitive band, and the chart set at a level is the view's. Highlights read all-time
        // (docs/design/seasons.md D6, D18).
        return await _cache.GetOrCreateAsync(
            CacheKeys.Viewer(nameof(HighlightCaptureSaga), mix, SeasonId.AllTime, "Cohort", userId, type, (int)level),
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
