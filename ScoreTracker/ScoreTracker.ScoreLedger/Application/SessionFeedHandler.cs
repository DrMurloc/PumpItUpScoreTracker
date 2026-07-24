using MediatR;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.ScoreLedger.Contracts;
using ScoreTracker.ScoreLedger.Contracts.Queries;
using ScoreTracker.ScoreLedger.Domain;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ScoreLedger.Application;

/// <summary>
///     The Sessions page's journal read: pages session groups and classifies every row
///     against the chart's prior journal state <em>in the same mix</em>. The journal is
///     complete back to the 2026-06 backfill, so "no prior row" genuinely means a first
///     entry. Mix scoping matters because Phoenix and Phoenix 2 share chart ids (a
///     returning song is one ChartId in both), so a first-ever Phoenix 2 play must read as
///     a New Pass, not an Upscore/Clear over the player's Phoenix 1 best.
/// </summary>
internal sealed class SessionFeedHandler : IRequestHandler<GetRecentSessionsQuery, RecentSessionsPage>
{
    private readonly IScoreJournalRepository _journal;
    private readonly IUserReader _users;
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IScoreReader _scores;

    public SessionFeedHandler(IScoreJournalRepository journal, IUserReader users, ICurrentUserAccessor currentUser,
        IScoreReader scores)
    {
        _journal = journal;
        _users = users;
        _currentUser = currentUser;
        _scores = scores;
    }

    public async Task<RecentSessionsPage> Handle(GetRecentSessionsQuery request,
        CancellationToken cancellationToken)
    {
        // Defense in depth behind the page's redirect: non-public players read as empty
        // to everyone but themselves.
        var user = await _users.GetUser(request.UserId, cancellationToken);
        var isOwner = _currentUser.IsLoggedIn && _currentUser.User.Id == request.UserId;
        if (user is not { IsPublic: true } && !isOwner)
            return new RecentSessionsPage(0, Array.Empty<RecentSessionsPage.SessionGroup>());

        var (total, groups) = await _journal.GetSessionGroups(request.UserId,
            Math.Max(1, request.Page), Math.Clamp(request.PageSize, 1, 50), cancellationToken);
        var chartIds = groups.SelectMany(g => g.Rows).Select(r => r.ChartId).Distinct().ToArray();
        var histories = (await _journal.GetChartHistories(request.UserId, chartIds,
                cancellationToken))
            .GroupBy(r => r.ChartId)
            .ToDictionary(g => g.Key, g => g.OrderBy(r => r.OccurredAt).ToArray());

        // Cross-mix reclear (parity with the Discord session card): a New Pass on a chart
        // already cleared non-broken in the OTHER Phoenix-family mix (free from the cross-mix
        // history) or in legacy XX. The XX bests load only when the page actually holds a New
        // Pass, so an upscore-only page skips the read entirely, like the card does.
        var anyNewPass = groups.Any(g => g.Rows.Any(r => !r.IsBroken
            && Classify(r, History(histories, r)).Classification == ScoreEventClassification.NewPass));
        var xxCleared = anyNewPass
            ? (await _scores.GetBestXXAttempts(request.UserId, cancellationToken))
                .Where(a => a.BestAttempt is { IsBroken: false })
                .Select(a => a.Chart.Id).ToHashSet()
            : new HashSet<Guid>();

        return new RecentSessionsPage(total, groups.Select(g => new RecentSessionsPage.SessionGroup(
                g.SessionId,
                g.Day,
                g.Mix,
                DominantSource(g.Rows),
                g.Rows.Min(r => r.OccurredAt),
                g.Rows.Max(r => r.OccurredAt),
                g.Rows.OrderByDescending(r => r.OccurredAt)
                    .Select(r => Classify(r, History(histories, r), xxCleared))
                    .ToArray()))
            .ToArray());
    }

    private static ScoreJournalEntry[] History(IReadOnlyDictionary<Guid, ScoreJournalEntry[]> histories,
        ScoreJournalEntry row)
    {
        return histories.GetValueOrDefault(row.ChartId, Array.Empty<ScoreJournalEntry>());
    }

    private static string DominantSource(IReadOnlyList<ScoreJournalEntry> rows)
    {
        return rows.GroupBy(r => r.Source).OrderByDescending(g => g.Count()).First().Key;
    }

    private static RecentSessionsPage.ScoreEventRecord Classify(ScoreJournalEntry row,
        ScoreJournalEntry[] chartHistory, IReadOnlySet<Guid>? xxCleared = null)
    {
        // Same-mix only: a returning song carries one ChartId across Phoenix and Phoenix 2,
        // so its Phoenix 1 history must not count as prior state for a Phoenix 2 play.
        var prior = chartHistory.Where(h => h.Mix == row.Mix && h.OccurredAt < row.OccurredAt).ToArray();
        var priorBest = prior.Where(p => p.Score != null).Select(p => (int?)(int)p.Score!.Value).Max();
        var priorPassed = prior.Any(p => !p.IsBroken);
        var priorBestPlate = prior.Where(p => !p.IsBroken && p.Plate != null).Select(p => p.Plate).Max();
        var classification = ClassifyRow(row, priorPassed, priorBest, priorBestPlate);

        // A New Pass on a chart cleared non-broken elsewhere is a reclear: the other
        // Phoenix-family mix shows up in the cross-mix history, legacy XX comes in via
        // xxCleared. Only new passes qualify — matching the Discord card's "* = reclears".
        var isReclear = classification == ScoreEventClassification.NewPass
                        && (chartHistory.Any(h => h.Mix != row.Mix && !h.IsBroken)
                            || (xxCleared?.Contains(row.ChartId) ?? false));

        return new RecentSessionsPage.ScoreEventRecord(row.ChartId, row.OccurredAt,
            row.Score == null ? null : (int)row.Score.Value, row.Plate?.ToString(), row.IsBroken, row.Source,
            row.SessionId, classification,
            classification == ScoreEventClassification.Upscore ? priorBest : null,
            isReclear);
    }

    private static ScoreEventClassification ClassifyRow(ScoreJournalEntry row, bool priorPassed, int? priorBest,
        PhoenixPlate? priorBestPlate)
    {
        if (row.IsBroken) return ScoreEventClassification.Break;
        if (!priorPassed) return ScoreEventClassification.NewPass;
        if (row.Score == null) return ScoreEventClassification.Played;
        var score = (int)row.Score.Value;
        if (priorBest == null || score > priorBest || (score == priorBest && row.Plate > priorBestPlate))
            return ScoreEventClassification.Upscore;
        return ScoreEventClassification.Played;
    }
}
