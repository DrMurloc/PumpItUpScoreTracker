using System.Diagnostics.CodeAnalysis;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using MediatR;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.PlayerProgress.Contracts.Queries;
using ScoreTracker.ScoreLedger.Contracts;
using ScoreTracker.ScoreLedger.Contracts.Queries;

namespace ScoreTracker.Web.Services;

/// <summary>
///     Assembles one session's breakdown from the verticals that own its parts.
///     <para>
///         This lives in Web rather than behind a single vertical query, and that is forced
///         rather than chosen: the pieces are spread across ScoreLedger (journal, sessions),
///         PlayerProgress (highlights, milestones, stats) and Catalog (charts), and no
///         vertical can reference all three — PlayerProgress sits upstream of ScoreLedger.
///         Composition of already-published contracts is presentation work, which is what
///         <c>Services/</c> is for; nothing here decides anything a vertical should own.
///     </para>
/// </summary>
public sealed class SessionBreakdownBuilder(IMediator mediator, IScoreReader ledger,
    IDateTimeOffsetAccessor clock)
{
    /// <summary>Jackets per card before the "+N" count takes over.</summary>
    private const int TopChartsPerCard = 3;

    /// <summary>First load: both halves.</summary>
    public async Task<SessionsPageModel> Build(Guid userId, Guid? selectedSessionId, int page, int pageSize,
        DateTimeOffset? before, CancellationToken cancellationToken)
    {
        var (feed, selected, undone) = await Select(userId, selectedSessionId, page, pageSize, before,
            cancellationToken);
        if (selected == null) return Empty;

        var sessions = await mediator.Send(new GetScoreSessionsQuery(userId), cancellationToken);
        return new SessionsPageModel(
            await BuildOne(userId, selected, sessions, cancellationToken),
            await BuildHistory(userId, feed.Groups, sessions, cancellationToken),
            feed.TotalGroups, undone);
    }

    /// <summary>
    ///     Promoting a card into the hero. The history rows are unchanged — only which one is
    ///     highlighted — so they are carried over rather than rebuilt, which is what keeps the
    ///     interaction from looking like a page load.
    /// </summary>
    public async Task<SessionsPageModel> Reselect(SessionsPageModel current, Guid userId, Guid sessionId,
        int page, int pageSize, DateTimeOffset? before, CancellationToken cancellationToken)
    {
        var (_, selected, undone) = await Select(userId, sessionId, page, pageSize, before, cancellationToken);
        if (selected == null) return current;

        var sessions = await mediator.Send(new GetScoreSessionsQuery(userId), cancellationToken);
        return current with
        {
            Hero = await BuildOne(userId, selected, sessions, cancellationToken),
            SelectedSessionWasUndone = undone
        };
    }

    /// <summary>
    ///     Paging or filtering the history. The hero is untouched — it is not what changed —
    ///     so none of its peer, chart or milestone reads run again.
    /// </summary>
    public async Task<SessionsPageModel> Refilter(SessionsPageModel current, Guid userId, int page,
        int pageSize, DateTimeOffset? before, CancellationToken cancellationToken)
    {
        var feed = await mediator.Send(new GetRecentSessionsQuery(userId, page, pageSize, before),
            cancellationToken);
        var sessions = await mediator.Send(new GetScoreSessionsQuery(userId), cancellationToken);
        return current with
        {
            History = await BuildHistory(userId, feed.Groups, sessions, cancellationToken),
            TotalGroups = feed.TotalGroups
        };
    }

    private static readonly SessionsPageModel Empty =
        new(null, Array.Empty<SessionHistoryRow>(), 0, false);

    /// <summary>
    ///     Which group the hero shows. A Discord card outlives the session it links to — undo
    ///     deletes the journal rows — so a deep link that finds nothing is a real state, not a
    ///     404-shaped hole (§2.3).
    /// </summary>
    private async Task<(RecentSessionsPage Feed, RecentSessionsPage.SessionGroup? Selected, bool Undone)> Select(
        Guid userId, Guid? selectedSessionId, int page, int pageSize, DateTimeOffset? before,
        CancellationToken cancellationToken)
    {
        var feed = await mediator.Send(new GetRecentSessionsQuery(userId, page, pageSize, before),
            cancellationToken);
        if (feed.Groups.Count == 0) return (feed, null, false);

        var selected = selectedSessionId == null
            ? feed.Groups[0]
            : feed.Groups.FirstOrDefault(g => g.SessionId == selectedSessionId);
        return (feed, selected ?? feed.Groups[0], selectedSessionId != null && selected == null);
    }

    private async Task<SessionBreakdown> BuildOne(Guid userId, RecentSessionsPage.SessionGroup group,
        IReadOnlyList<ScoreSessionRecord> sessions, CancellationToken cancellationToken)
    {
        var chartIds = group.Rows.Select(r => r.ChartId).Distinct().ToArray();
        var charts = (await mediator.Send(new GetChartsQuery(group.Mix, ChartIds: chartIds), cancellationToken))
            .ToDictionary(c => c.Id);

        var highlights = group.SessionId == null
            ? Array.Empty<ScoreHighlightRecord>()
            : (await mediator.Send(new GetScoreHighlightsForSessionsQuery(userId, new[] { group.SessionId.Value }),
                cancellationToken)).ToArray();
        var milestones = group.SessionId == null
            ? Array.Empty<PlayerMilestoneRecord>()
            : (await mediator.Send(new GetPlayerMilestonesForSessionsQuery(userId, new[] { group.SessionId.Value }),
                cancellationToken)).ToArray();

        var pinned = PinHighlights(group.Rows, highlights);
        var phoenix1 = await Phoenix1Bests(userId, group.Mix, chartIds, cancellationToken);
        var scores = group.Rows
            .Select((r, index) => new SessionScore(r, charts.GetValueOrDefault(r.ChartId),
                pinned[index].Flags, pinned[index].Detail, Phoenix1Gain(r, phoenix1)))
            .ToArray();

        var stats = await mediator.Send(new GetPlayerStatsQuery(userId, group.Mix), cancellationToken);
        var session = sessions.FirstOrDefault(s => s.Id == group.SessionId);
        return new SessionBreakdown(group, session, charts, scores,
            BuildCeremony(milestones, stats),
            milestones.Where(m => m.Kind != MilestoneKind.TitleProgress).ToArray(),
            BuildTitleBars(milestones),
            CaptureWindowOpen(session),
            highlights.Length + milestones.Length);
    }

    /// <summary>
    ///     How long after the scores land the page keeps expecting capture. Taken from the
    ///     Ledger's own batching policy rather than chosen: scores are held as a batch and
    ///     capture cannot start until it drains, so any duration invented here is a guess about
    ///     someone else's timer.
    ///     <para>
    ///         ⚠ It was originally two minutes, which was the same number for the wrong reason —
    ///         the hold window exactly, so this expired at the instant the batch fired and the
    ///         page gave up a heartbeat before its data arrived, every time. The hold is also
    ///         measured from the LATEST score, so a long import pushes it out repeatedly.
    ///     </para>
    /// </summary>
    private static readonly TimeSpan CaptureWindow = ScoreBatchPolicy.WorkExpectedWithin;

    /// <summary>
    ///     Whether the scores arrived recently enough that capture could still be running. This
    ///     says nothing about whether it HAS run — deliberately, because those are different
    ///     questions and conflating them is what broke this twice.
    ///     <para>
    ///         Capture writes in several passes (flags, then folder lamps, then the rating step
    ///         that produces the competitive baseline and the PUMBILITY gain, then titles), so a
    ///         read taken between two of them sees rows without seeing all of them. A page that
    ///         opened at that moment therefore has rows, shows no card — and must STILL watch,
    ///         or it sits on half a session until someone reloads by hand. Whether to watch is
    ///         this; whether to show the card is <see cref="SessionBreakdown.CapturePending" />;
    ///         when to stop is the row count going quiet.
    ///     </para>
    ///     <para>
    ///         Sessions predating the ScoreSession table have no wall clock to test, so their
    ///         window is never open: they are historical by definition.
    ///     </para>
    /// </summary>
    private bool CaptureWindowOpen(ScoreSessionRecord? session)
    {
        return session != null && clock.Now - session.LastActivityAt < CaptureWindow;
    }

    /// <summary>
    ///     The player's Phoenix 1 best on each of the session's charts, for the "you passed your
    ///     Phoenix 1 self" mark. Only a Phoenix 2 session can say it, so a Phoenix session pays
    ///     nothing. Chart-scoped rather than the whole Phoenix record set: a session touches
    ///     twenty charts and a long-standing player owns thousands.
    ///     <para>
    ///         Broken records are skipped on purpose — the app never rates a broken attempt
    ///         (a walk-off's partial score is not a result you would claim), so it is not a
    ///         "best" to have passed either.
    ///     </para>
    /// </summary>
    private async Task<IReadOnlyDictionary<Guid, int>> Phoenix1Bests(Guid userId, MixEnum mix,
        IReadOnlyList<Guid> chartIds, CancellationToken cancellationToken)
    {
        if (mix != MixEnum.Phoenix2 || chartIds.Count == 0) return new Dictionary<Guid, int>();

        return (await ledger.GetPlayerScores(MixEnum.Phoenix, new[] { userId }, chartIds, cancellationToken))
            .Where(s => !s.IsBroken)
            .GroupBy(s => s.ChartId)
            .ToDictionary(g => g.Key, g => g.Max(s => (int)s.Score));
    }

    /// <summary>
    ///     How far this play went past the player's Phoenix 1 best — and only the first time it
    ///     does. Once a previous Phoenix 2 score already cleared that bar the mark is spent: it
    ///     is about the moment you passed your old self, not a standing comparison that would
    ///     then ride every later upscore on the same chart.
    ///     <para>
    ///         A new pass carries no <c>PreviousBest</c>, and that is exactly the case the mark
    ///         is for — nothing stood here before, so anything above Phoenix 1 clears it.
    ///     </para>
    /// </summary>
    private static int? Phoenix1Gain(RecentSessionsPage.ScoreEventRecord row,
        IReadOnlyDictionary<Guid, int> phoenix1)
    {
        if (row.IsBroken || row.Score is not { } score) return null;
        if (!phoenix1.TryGetValue(row.ChartId, out var best)) return null;
        if (row.PreviousBest >= best) return null;

        return score > best ? score - best : null;
    }

    /// <summary>
    ///     A session is many two-minute batches inside an eight-hour envelope, so a stat can move
    ///     several times. The band shows the whole session's travel: earliest old, latest new.
    /// </summary>
    private static SessionCeremony BuildCeremony(IReadOnlyList<PlayerMilestoneRecord> milestones,
        PlayerStatsRecord stats)
    {
        (double? Old, double? New) Span(MilestoneKind kind)
        {
            var rows = milestones.Where(m => m.Kind == kind).OrderBy(m => m.OccurredAt).ToArray();
            return rows.Length == 0 ? (null, null) : (rows.First().OldValue, rows.Last().NewValue);
        }

        var pumbility = Span(MilestoneKind.PumbilityGain);
        var singles = Span(MilestoneKind.SinglesCompetitiveGain);
        var doubles = Span(MilestoneKind.DoublesCompetitiveGain);
        var rank = milestones.Where(m => m.Kind == MilestoneKind.OfficialPumbilityRank
                                         && m.Detail == OfficialPumbilityBoardNames.Combined)
            .OrderBy(m => m.OccurredAt).ToArray();

        return new SessionCeremony(stats.SkillRating, pumbility.Old, pumbility.New,
            singles.Old, singles.New, doubles.Old, doubles.New,
            stats.SinglesCompetitiveLevel, stats.DoublesCompetitiveLevel,
            stats.EstimatedPumbilityRank,
            rank.Length == 0 ? null : (int?)rank.First().OldValue,
            stats.PumbilityBoardAsOf);
    }

    /// <summary>
    ///     One bar per scope, the title being worked on in it. A scope can be nudged by several
    ///     batches, so the bar spans the session: earliest old, latest new.
    /// </summary>
    private static IReadOnlyList<SessionTitleBarModel> BuildTitleBars(
        IReadOnlyList<PlayerMilestoneRecord> milestones)
    {
        return milestones
            .Where(m => m.Kind == MilestoneKind.TitleProgress && m.Detail != null && m.Title != null)
            .Select(m => (Milestone: m, Parts: m.Detail!.Split('|')))
            .Where(x => x.Parts.Length == 3)
            .GroupBy(x => x.Parts[0])
            .Select(scope =>
            {
                var ordered = scope.OrderBy(x => x.Milestone.OccurredAt).ToArray();
                var latest = ordered.Last();
                return new SessionTitleBarModel(scope.Key, latest.Milestone.Title!,
                    ordered.First().Milestone.OldValue ?? 0, latest.Milestone.NewValue ?? 0,
                    int.TryParse(latest.Parts[1], out var current) ? current : 0,
                    int.TryParse(latest.Parts[2], out var required) ? required : 0);
            })
            .OrderByDescending(b => b.NewPercent)
            .ToArray();
    }

    /// <summary>
    ///     The grid's rows. Charts and milestones load once for the whole page rather than per
    ///     card — a card that fetched its own art would put a query per session on every page
    ///     turn.
    /// </summary>
    private async Task<IReadOnlyList<SessionHistoryRow>> BuildHistory(Guid userId,
        IReadOnlyList<RecentSessionsPage.SessionGroup> groups, IReadOnlyList<ScoreSessionRecord> sessions,
        CancellationToken cancellationToken)
    {
        var charts = new Dictionary<Guid, Chart>();
        foreach (var byMix in groups.GroupBy(g => g.Mix))
        {
            var ids = byMix.SelectMany(g => g.Rows).Select(r => r.ChartId).Distinct().ToArray();
            if (ids.Length == 0) continue;
            foreach (var chart in await mediator.Send(new GetChartsQuery(byMix.Key, ChartIds: ids),
                         cancellationToken))
                charts[chart.Id] = chart;
        }

        var sessionIds = groups.Where(g => g.SessionId != null).Select(g => g.SessionId!.Value).ToArray();
        var milestones = sessionIds.Length == 0
            ? Array.Empty<PlayerMilestoneRecord>()
            : (await mediator.Send(new GetPlayerMilestonesForSessionsQuery(userId, sessionIds), cancellationToken))
                .ToArray();

        return groups.Select(g => ToHistoryRow(g, sessions, charts, milestones)).ToArray();
    }

    /// <summary>
    ///     The headline is what answers "was anything good in there" without opening the card.
    ///     Titles earned lead, then a PUMBILITY gain, then folder lamps — and there is
    ///     deliberately no filler when a session has none, because most sessions predate
    ///     capture and a card that insists on a headline would look broken on all of them.
    /// </summary>
    private static readonly MilestoneKind[] HeadlineOrder =
    {
        MilestoneKind.TitleCompleted, MilestoneKind.PumbilityGain, MilestoneKind.FolderPassLamp,
        MilestoneKind.FolderGradeLamp, MilestoneKind.FolderPlateLamp
    };

    private const int HeadlineCap = 2;

    private static SessionHistoryRow ToHistoryRow(RecentSessionsPage.SessionGroup group,
        IReadOnlyList<ScoreSessionRecord> sessions, IReadOnlyDictionary<Guid, Chart> charts,
        IReadOnlyList<PlayerMilestoneRecord> milestones)
    {
        // Counts come denormalized off the session row where one exists and off the journal
        // where it does not — sessions predate that table and the page keeps them all.
        var session = sessions.FirstOrDefault(s => s.Id == group.SessionId);
        var played = group.Rows
            .Select(r => charts.GetValueOrDefault(r.ChartId))
            .Where(c => c != null)
            .Select(c => c!)
            .DistinctBy(c => c.Id)
            .OrderByDescending(c => (int)c.Level)
            .ToArray();

        var headline = milestones
            .Where(m => m.SessionId == group.SessionId && Array.IndexOf(HeadlineOrder, m.Kind) >= 0)
            .OrderBy(m => Array.IndexOf(HeadlineOrder, m.Kind))
            .Take(HeadlineCap)
            .ToArray();

        return new SessionHistoryRow(group.SessionId, group.Day, group.Mix, group.Source,
            group.Start, group.End,
            session?.NewCount ?? group.Rows.Count(r => r.Classification == ScoreEventClassification.NewPass),
            session?.UpscoreCount ?? group.Rows.Count(r => r.Classification == ScoreEventClassification.Upscore),
            session?.ScoreCount ?? group.Rows.Count,
            played.Take(TopChartsPerCard).ToArray(),
            Math.Max(0, played.Length - TopChartsPerCard),
            LevelSpan(played),
            headline,
            session?.AccountTag);
    }

    /// <summary>
    ///     "S17–S22 · D19–D23". What separates a warm-up from a session at the player's
    ///     ceiling — two sessions of forty plays are not the same session.
    /// </summary>
    private static string LevelSpan(IReadOnlyList<Chart> played)
    {
        return string.Join(" · ", played
            .GroupBy(c => c.Type)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var low = g.Min(c => (int)c.Level);
                var high = g.Max(c => (int)c.Level);
                var tag = g.Key.GetShortHand();
                return low == high ? $"{tag}{low}" : $"{tag}{low}–{tag}{high}";
            }));
    }

    /// <summary>
    ///     Pins each captured highlight to the journal row that earned it (D45). Capture writes
    ///     one row per (session, chart) per batch, computed on the record change that batch
    ///     carried — so a chart's captures (in time order) belong to its NewPass/Upscore rows
    ///     (in time order), aligned at the end, because a batch that saw both the pass and an
    ///     upscore captured once, on the final state. Every other row of the chart — breaks,
    ///     repeats, observations — carries nothing: stamping the merged flags across the chart
    ///     is how four stage breaks each wore the pass's official-board medal.
    ///     <para>
    ///         A capture whose chart has no record-changing row shows nowhere rather than on a
    ///         break, and a race-duplicated capture can still land on one extra record row —
    ///         never on an attempt, which is the failure that mattered.
    ///     </para>
    /// </summary>
    private static (HighlightFlags Flags, HighlightDetail? Detail)[] PinHighlights(
        IReadOnlyList<RecentSessionsPage.ScoreEventRecord> rows, IReadOnlyList<ScoreHighlightRecord> highlights)
    {
        var pinned = new (HighlightFlags Flags, HighlightDetail? Detail)[rows.Count];
        foreach (var chart in highlights.GroupBy(h => h.ChartId))
        {
            var earners = rows
                .Select((row, index) => (row, index))
                .Where(x => x.row.ChartId == chart.Key
                            && x.row.Classification is ScoreEventClassification.NewPass
                                or ScoreEventClassification.Upscore)
                .OrderBy(x => x.row.OccurredAt)
                .ToArray();
            if (earners.Length == 0) continue;

            var captured = chart.OrderBy(h => h.OccurredAt).ToArray();
            for (var i = 0; i < captured.Length; i++)
            {
                var at = earners[Math.Max(0, earners.Length - captured.Length + i)].index;
                pinned[at] = (pinned[at].Flags | captured[i].Flags,
                    RicherDetail(pinned[at].Detail, captured[i].Detail));
            }
        }

        return pinned;
    }

    /// <summary>Ties go to the newer capture: it describes the later state of the chart.</summary>
    private static HighlightDetail? RicherDetail(HighlightDetail? older, HighlightDetail? newer)
    {
        return DetailFields(newer) >= DetailFields(older) ? newer : older;
    }

    private static int DetailFields(HighlightDetail? detail)
    {
        if (detail == null) return 0;
        return new object?[]
        {
            detail.PumbilityRank, detail.FolderDebutOrdinal, detail.PeerCount, detail.PeerPercentile,
            detail.AttemptsBeforeClear, detail.OfficialPlace
        }.Count(x => x != null);
    }
}

/// <summary>The whole page: one hero, the rest as rows.</summary>
[ExcludeFromCodeCoverage]
public sealed record SessionsPageModel(
    SessionBreakdown? Hero,
    IReadOnlyList<SessionHistoryRow> History,
    int TotalGroups,
    bool SelectedSessionWasUndone);
