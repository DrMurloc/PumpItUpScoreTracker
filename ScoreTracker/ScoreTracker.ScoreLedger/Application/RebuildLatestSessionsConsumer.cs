using MassTransit;
using Microsoft.Extensions.Logging;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.ScoreLedger.Contracts;
using ScoreTracker.ScoreLedger.Contracts.Messages;
using ScoreTracker.ScoreLedger.Domain;

namespace ScoreTracker.ScoreLedger.Application;

/// <summary>
///     Replays each player's most recent session through the live pipeline by republishing the
///     score event it would have produced. Deliberately not a second capture path: the flags,
///     details and milestones all come out of the same consumer that writes them normally, so
///     a rebuilt session cannot drift from a captured one.
///     <para>
///         Lives in ScoreLedger because reconstructing the event needs the journal, and the
///         journal is ledger-internal.
///     </para>
/// </summary>
internal sealed class RebuildLatestSessionsConsumer(
        IScoreSessionRepository sessions,
        IScoreJournalRepository journal,
        IDateTimeOffsetAccessor dateTime,
        IBus bus,
        ILogger<RebuildLatestSessionsConsumer> logger)
    : IConsumer<RebuildLatestSessionsCommand>
{
    public async Task Consume(ConsumeContext<RebuildLatestSessionsCommand> context)
    {
        var latest = await sessions.ListLatestPerUser(context.CancellationToken);
        var rebuilt = 0;
        foreach (var session in latest)
            try
            {
                var changes = await ChangesFor(session, context.CancellationToken);
                if (changes.Length == 0) continue;
                await bus.Publish(PlayerScoresUpdatedEvent.Create(dateTime.Now, session.UserId, session.Mix,
                    changes, session.Id), context.CancellationToken);
                rebuilt++;
            }
            catch (Exception ex)
            {
                // One player's bad session must not cost everyone else's rebuild.
                logger.LogError(ex, "Rebuild failed for session {SessionId} (user {UserId})",
                    session.Id, session.UserId);
            }

        logger.LogInformation("Rebuilt {Count} of {Total} latest sessions", rebuilt, latest.Count);
    }

    /// <summary>
    ///     The change set the session produced, reconstructed from the journal: each chart's
    ///     best row in the session, against whatever stood before the session opened.
    /// </summary>
    private async Task<PlayerScoresUpdatedEvent.ScoreChange[]> ChangesFor(ScoreSessionRecord session,
        CancellationToken cancellationToken)
    {
        var rows = await journal.GetSessionEntries(session.UserId, session.Id, cancellationToken);
        var chartIds = rows.Select(r => r.ChartId).Distinct().ToArray();
        if (chartIds.Length == 0) return Array.Empty<PlayerScoresUpdatedEvent.ScoreChange>();

        var histories = (await journal.GetChartHistories(session.UserId, chartIds, cancellationToken))
            .GroupBy(h => h.ChartId)
            .ToDictionary(g => g.Key, g => g.ToArray());

        return rows
            .Where(r => r.IsBest)
            .GroupBy(r => r.ChartId)
            .Select(chart =>
            {
                var best = chart.OrderByDescending(r => r.OccurredAt).First();
                // Same-mix only: a returning song carries one ChartId across Phoenix and
                // Phoenix 2, so the other mix's history is not prior state here.
                var before = histories.GetValueOrDefault(chart.Key, Array.Empty<ScoreJournalEntry>())
                    .Where(h => h.IsBest && h.Mix == session.Mix && h.OccurredAt < chart.Min(r => r.OccurredAt))
                    .ToArray();
                var oldScore = before.Where(h => h.Score != null)
                    .Select(h => (int?)(int)h.Score!.Value).Max();
                return new PlayerScoresUpdatedEvent.ScoreChange(chart.Key,
                    !before.Any(h => !h.IsBroken),
                    oldScore,
                    best.Score == null ? null : (int)best.Score.Value,
                    best.Plate?.ToString(),
                    best.IsBroken);
            })
            .ToArray();
    }
}
