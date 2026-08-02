using System.Diagnostics.CodeAnalysis;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using MediatR;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.Communities.Contracts.Queries;
using ScoreTracker.Domain.Models;
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
///         PlayerProgress (highlights, milestones, stats), Communities (peers) and Catalog
///         (charts), and no vertical can reference all four — PlayerProgress sits upstream of
///         both ScoreLedger and Communities. Composition of already-published contracts is
///         presentation work, which is what <c>Services/</c> is for; nothing here decides
///         anything a vertical should own.
///     </para>
/// </summary>
public sealed class SessionBreakdownBuilder(IMediator mediator)
{
    /// <summary>How many charts get a community-peer board. Every flagged chart qualifies (D9).</summary>
    private const int MaxPeerBoards = 8;

    /// <summary>
    ///     Names per board. A big club puts dozens of people on a popular chart, and a list that
    ///     long stops being a comparison and starts being a directory — the nearest few are the
    ///     ones the section is actually about. The full board is one tap away in the dialog.
    /// </summary>
    private const int MaxPeersPerBoard = 5;

    /// <summary>Jackets per card before the "+N" count takes over.</summary>
    private const int TopChartsPerCard = 3;

    public async Task<SessionsPageModel> Build(Guid userId, Guid? selectedSessionId, int page, int pageSize,
        DateTimeOffset? before, CancellationToken cancellationToken)
    {
        var feed = await mediator.Send(new GetRecentSessionsQuery(userId, page, pageSize, before),
            cancellationToken);
        if (feed.Groups.Count == 0)
            return new SessionsPageModel(null, Array.Empty<SessionHistoryRow>(), 0, false);

        // A Discord card outlives the session it links to — undo deletes the journal rows — so
        // a deep link that finds nothing is a real state, not a 404-shaped hole (§2.3).
        var selected = selectedSessionId == null
            ? feed.Groups[0]
            : feed.Groups.FirstOrDefault(g => g.SessionId == selectedSessionId);
        var undone = selectedSessionId != null && selected == null;
        selected ??= feed.Groups[0];

        var sessions = await mediator.Send(new GetScoreSessionsQuery(userId), cancellationToken);
        var history = await BuildHistory(userId, feed.Groups, sessions, cancellationToken);
        var breakdown = await BuildOne(userId, selected, sessions, cancellationToken);
        return new SessionsPageModel(breakdown, history, feed.TotalGroups, undone);
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

        // One highlight row per (session, chart) is the norm; a race can duplicate it, so keep
        // the richest detail rather than whichever arrived first.
        var byChart = highlights
            .GroupBy(h => h.ChartId)
            .ToDictionary(g => g.Key, g => (
                Flags: g.Aggregate(HighlightFlags.None, (f, h) => f | h.Flags),
                Detail: g.OrderByDescending(h => DetailFields(h.Detail)).First().Detail));

        var scores = group.Rows
            .Select(r =>
            {
                var captured = byChart.GetValueOrDefault(r.ChartId);
                return new SessionScore(r, charts.GetValueOrDefault(r.ChartId), captured.Flags, captured.Detail);
            })
            .ToArray();

        var stats = await mediator.Send(new GetPlayerStatsQuery(userId, group.Mix), cancellationToken);
        return new SessionBreakdown(group, sessions.FirstOrDefault(s => s.Id == group.SessionId), charts, scores,
            BuildCeremony(milestones, stats),
            milestones.Where(m => m.Kind != MilestoneKind.TitleProgress).ToArray(),
            BuildTitleBars(milestones),
            await BuildPeerBoards(userId, group, scores, charts, stats, cancellationToken));
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

    private async Task<IReadOnlyList<SessionPeerBoard>> BuildPeerBoards(Guid userId,
        RecentSessionsPage.SessionGroup group, IReadOnlyList<SessionScore> scores,
        IReadOnlyDictionary<Guid, Chart> charts, PlayerStatsRecord stats,
        CancellationToken cancellationToken)
    {
        // Distinct FIRST: `scores` is one entry per journal row, and a chart played several
        // times in a session has several. That is not an edge case — it is exactly what a
        // session with attempts looks like, which is the feature this section sits next to.
        var wanted = scores
            .Where(s => s.IsFlagged && s.Chart != null)
            .GroupBy(s => s.Row.ChartId)
            .OrderByDescending(g => (int)g.First().Chart!.Level)
            .Select(g => g.Key)
            .Take(MaxPeerBoards)
            .ToArray();
        if (wanted.Length == 0) return Array.Empty<SessionPeerBoard>();

        var peers = await mediator.Send(new GetCommunityPeerScoresQuery(userId, group.Mix, wanted),
            cancellationToken);

        return wanted
            .Where(id => peers.ContainsKey(id) && charts.ContainsKey(id))
            .Select(id => new SessionPeerBoard(charts[id], peers[id]
                // Closeness sorts, it never filters — a clubmate three levels away is still a
                // clubmate (D8). Both sides of this subtraction have to be the same quantity:
                // the peer's competitive level against MINE, read for the chart's own type,
                // which is how Communities computed theirs.
                .OrderBy(p => Math.Abs(p.CompetitiveLevel - MyCompetitiveLevel(charts[id], stats)))
                .ThenByDescending(p => (int)p.Score)
                .Take(MaxPeersPerBoard)
                .ToArray()))
            .ToArray();
    }

    private static double MyCompetitiveLevel(Chart chart, PlayerStatsRecord stats)
    {
        return chart.Type switch
        {
            ChartType.Single => stats.SinglesCompetitiveLevel,
            ChartType.Double => stats.DoublesCompetitiveLevel,
            _ => stats.CompetitiveLevel
        };
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
