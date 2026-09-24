using MediatR;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.ScoreLedger.Contracts;
using ScoreTracker.ScoreLedger.Contracts.Queries;
using ScoreTracker.ScoreLedger.Domain;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ScoreLedger.Application;

/// <summary>
///     The Sessions page's journal read: pages sessions (stored sessions folded on the eight-hour
///     rule, docs/design/session-breakdown.md §8) and classifies every row against the chart's
///     prior journal state <em>in the same mix</em>. The journal is complete back to the 2026-06
///     backfill, so "no prior row" genuinely means a first entry. Mix scoping matters because
///     Phoenix and Phoenix 2 share chart ids (a returning song is one ChartId in both), so a
///     first-ever Phoenix 2 play must read as a New Pass, not an Upscore/Clear over the player's
///     Phoenix 1 best.
/// </summary>
internal sealed class SessionFeedHandler : IRequestHandler<GetRecentSessionsQuery, RecentSessionsPage>,
    IRequestHandler<GetSessionContainingQuery, RecentSessionsPage.SessionGroup?>
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
        if (!await CanRead(request.UserId, cancellationToken))
            return new RecentSessionsPage(0, Array.Empty<RecentSessionsPage.SessionGroup>());

        var (total, groups) = await _journal.GetSessionGroups(request.UserId,
            Math.Max(1, request.Page), Math.Clamp(request.PageSize, 1, GetRecentSessionsQuery.MaxPageSize),
            request.Before, cancellationToken);
        return new RecentSessionsPage(total, await ClassifyGroups(request.UserId, groups, cancellationToken));
    }

    public async Task<RecentSessionsPage.SessionGroup?> Handle(GetSessionContainingQuery request,
        CancellationToken cancellationToken)
    {
        if (!await CanRead(request.UserId, cancellationToken)) return null;

        var group = await _journal.GetSessionGroupContaining(request.UserId, request.SessionId,
            cancellationToken);
        return group == null
            ? null
            : (await ClassifyGroups(request.UserId, new[] { group }, cancellationToken)).FirstOrDefault();
    }

    /// <summary>
    ///     Defense in depth behind the page's redirect: a non-public player's sessions read as
    ///     nothing to everyone but themselves.
    /// </summary>
    private async Task<bool> CanRead(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _users.GetUser(userId, cancellationToken);
        var isOwner = _currentUser.IsLoggedIn && _currentUser.User.Id == userId;
        return user is { IsPublic: true } || isOwner;
    }

    private async Task<IReadOnlyList<RecentSessionsPage.SessionGroup>> ClassifyGroups(Guid userId,
        IReadOnlyList<JournalSessionRows> groups, CancellationToken cancellationToken)
    {
        var chartIds = groups.SelectMany(g => g.Rows).Select(r => r.ChartId).Distinct().ToArray();
        var histories = (await _journal.GetChartHistories(userId, chartIds,
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
            ? (await _scores.GetBestXXAttempts(userId, cancellationToken))
                .Where(a => a.BestAttempt is { IsBroken: false })
                .Select(a => a.Chart.Id).ToHashSet()
            : new HashSet<Guid>();

        // The keys and the rows are two reads, so an undo landing between them can leave a session
        // with nothing in it. It is gone; drop it rather than take the page down asking its span.
        return groups.Where(g => g.Rows.Count > 0).Select(g => new RecentSessionsPage.SessionGroup(
                g.SessionId,
                g.SessionIds,
                g.Day,
                g.Mix,
                DominantSource(g.Rows),
                g.Rows.Min(r => r.OccurredAt),
                g.Rows.Max(r => r.OccurredAt),
                g.Rows.OrderByDescending(r => r.OccurredAt)
                    .Select(r => Classify(r, History(histories, r), xxCleared))
                    .ToArray()))
            .ToArray();
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
        // so its Phoenix 1 history must not count as prior state for a Phoenix 2 play. Only
        // rows that were the record count as prior state — a losing attempt in the journal
        // never moved the best, so it must not make the next genuine clear read as an upscore.
        var prior = chartHistory
            .Where(h => h.IsBest && h.Mix == row.Mix && h.OccurredAt < row.OccurredAt)
            .ToArray();
        // The bar is the best PASS: a pass outranks any break whatever the numbers
        // (BestAttemptPolicy), so a failed attempt that scored higher is not what a later pass has
        // to beat — counting it read a genuine upscore as a plain play.
        var passes = prior.Where(p => !p.IsBroken).ToArray();
        var priorBest = passes.Where(p => p.Score != null).Select(p => (int?)(int)p.Score!.Value).Max();
        var priorPassed = passes.Length > 0;
        var priorBestPlate = passes.Where(p => p.Plate != null).Select(p => p.Plate).Max();
        var classification = ClassifyRow(row, priorPassed, priorBest, priorBestPlate);

        // A New Pass on a chart cleared non-broken elsewhere is a reclear: the other
        // Phoenix-family mix shows up in the cross-mix history, legacy XX comes in via
        // xxCleared. Only new passes qualify — matching the Discord card's "* = reclears".
        var isReclear = classification == ScoreEventClassification.NewPass
                        && (chartHistory.Any(h => h.IsBest && h.Mix != row.Mix && !h.IsBroken)
                            || (xxCleared?.Contains(row.ChartId) ?? false));

        // A stage break carries its flag and how many notes it judged, so the row can say how
        // far the run got; the classification stays Played — it moved nothing, like any
        // observation.
        return new RecentSessionsPage.ScoreEventRecord(row.ChartId, row.OccurredAt,
            row.Score == null ? null : (int)row.Score.Value, row.Plate?.ToString(), row.IsBroken, row.Source,
            row.SessionId, classification,
            // Every row carries it, not only upscores: "+N over P1" is spent once an earlier pass
            // reached the Phoenix 1 best, and a repeat needs the same bar to say so.
            priorBest,
            isReclear, row.IsStageBroken, row.Judgements?.NoteCount, row.Judgements,
            row.Cause.IsNonLifebarBreak, row.Cause.PassPlate?.GetName(), row.Cause.PassGrade?.GetName(),
            row.Cause.IsWalkOff, PlayNumber(row, chartHistory));
    }

    /// <summary>
    ///     Where this play falls among every play of its chart in its mix, itself included:
    ///     records, plays that never beat one, and stage breaks all count. The history is
    ///     cross-mix — a returning song keeps one ChartId — so the mix filter is what keeps
    ///     Phoenix 1's plays out of a Phoenix 2 number.
    /// </summary>
    private static int PlayNumber(ScoreJournalEntry row, ScoreJournalEntry[] chartHistory)
    {
        return chartHistory.Count(h => h.Mix == row.Mix && h.OccurredAt <= row.OccurredAt);
    }

    private static ScoreEventClassification ClassifyRow(ScoreJournalEntry row, bool priorPassed, int? priorBest,
        PhoenixPlate? priorBestPlate)
    {
        // A play the journal recorded as an observation never became the record, so it is a
        // play and nothing more — no history walk can promote it.
        if (!row.IsBest) return ScoreEventClassification.Played;
        if (row.IsBroken) return ScoreEventClassification.Break;
        if (!priorPassed) return ScoreEventClassification.NewPass;
        if (row.Score == null) return ScoreEventClassification.Played;
        var score = (int)row.Score.Value;
        if (priorBest == null || score > priorBest || (score == priorBest && row.Plate > priorBestPlate))
            return ScoreEventClassification.Upscore;
        return ScoreEventClassification.Played;
    }
}
