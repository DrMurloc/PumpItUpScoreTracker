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

    public async Task<SessionsPageModel> Build(Guid userId, Guid? selectedSessionId, int page, int pageSize,
        CancellationToken cancellationToken)
    {
        var feed = await mediator.Send(new GetRecentSessionsQuery(userId, page, pageSize), cancellationToken);
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
        var history = feed.Groups.Select(g => ToHistoryRow(g, sessions)).ToArray();
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

        return new SessionCeremony(pumbility.Old, pumbility.New,
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

    private static SessionHistoryRow ToHistoryRow(RecentSessionsPage.SessionGroup group,
        IReadOnlyList<ScoreSessionRecord> sessions)
    {
        // Counts come denormalized off the session row where one exists and off the journal
        // where it does not — sessions predate that table and the page keeps them all.
        var session = sessions.FirstOrDefault(s => s.Id == group.SessionId);
        return new SessionHistoryRow(group.SessionId, group.Day, group.Mix, group.Source, group.End,
            session?.NewCount ?? group.Rows.Count(r => r.Classification == ScoreEventClassification.NewPass),
            session?.UpscoreCount ?? group.Rows.Count(r => r.Classification == ScoreEventClassification.Upscore),
            session?.ScoreCount ?? group.Rows.Count);
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
