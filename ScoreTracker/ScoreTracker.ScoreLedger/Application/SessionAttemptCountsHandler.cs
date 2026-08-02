using MediatR;
using ScoreTracker.Domain.Records;
using ScoreTracker.ScoreLedger.Contracts.Queries;
using ScoreTracker.ScoreLedger.Domain;

namespace ScoreTracker.ScoreLedger.Application;

/// <summary>
///     Counts the losing attempts that preceded a clear inside one session. Everything happens
///     over the session's own rows — the journal read is already one query, and the arithmetic
///     is a group-by, so there is no repository method to add.
/// </summary>
internal sealed class SessionAttemptCountsHandler(IScoreJournalRepository journal)
    : IRequestHandler<GetSessionAttemptCountsQuery, IReadOnlyDictionary<Guid, int>>
{
    public async Task<IReadOnlyDictionary<Guid, int>> Handle(GetSessionAttemptCountsQuery request,
        CancellationToken cancellationToken)
    {
        var wanted = request.ChartIds.ToHashSet();
        if (wanted.Count == 0) return new Dictionary<Guid, int>();

        var rows = await journal.GetSessionEntries(request.UserId, request.SessionId, cancellationToken);
        var counts = new Dictionary<Guid, int>();
        foreach (var chart in rows.Where(r => wanted.Contains(r.ChartId)).GroupBy(r => r.ChartId))
        {
            var ordered = chart.OrderBy(r => r.OccurredAt).ToArray();
            var clearedAt = Array.FindIndex(ordered, IsClear);
            // Never cleared here, or cleared on the first play — nothing to say either way.
            if (clearedAt <= 0) continue;
            counts[chart.Key] = clearedAt;
        }

        return counts;
    }

    // A pass, by the same rule the record uses: unbroken with a score behind it. A broken row
    // is an attempt no matter how good the number on it was.
    private static bool IsClear(ScoreJournalEntry entry)
    {
        return !entry.IsBroken && entry.Score != null;
    }
}
