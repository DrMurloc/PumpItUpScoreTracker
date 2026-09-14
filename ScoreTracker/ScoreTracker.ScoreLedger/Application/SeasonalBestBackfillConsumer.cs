using MassTransit;
using Microsoft.Extensions.Logging;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.PlayerProgress.Contracts.Messages;
using ScoreTracker.ScoreLedger.Domain;
using ScoreTracker.Seasons.Contracts.Events;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ScoreLedger.Application;

/// <summary>
///     Rebuilds one season's pool from the journal (docs/design/seasons.md D23): every official
///     import play inside the window, replayed through the very writer a live import uses. That reuse
///     is the point — a backfilled season and a tracked one are the same rows produced by the same
///     rule, so the boards cannot disagree about which of the two a player's history came from.
///     <para>
///         Only the date clause of the counting rule can apply here: whether a card raised a record
///         is import-time knowledge the journal does not keep (D15). A pre-season play that was
///         upscored in-season is therefore missed by the backfill and caught by live tracking, which
///         is the same asymmetry the undo replay carries.
///     </para>
/// </summary>
internal sealed class SeasonalBestBackfillConsumer(IScoreJournalRepository journal, SeasonalBestWriter seasonalBests,
    ILogger<SeasonalBestBackfillConsumer> logger) : IConsumer<SeasonBackfillRequestedEvent>
{
    /// <summary>Phoenix 2 only: Phoenix 1 has no seasons and never will (§1).</summary>
    private static readonly MixEnum[] SeasonedMixes = { MixEnum.Phoenix2 };

    public async Task Consume(ConsumeContext<SeasonBackfillRequestedEvent> context)
    {
        var e = context.Message;
        foreach (var mix in SeasonedMixes)
        {
            var userIds = await journal.GetUsersWithPlaysInWindow(mix, e.StartsAt, e.EndsAt,
                context.CancellationToken);
            var replayed = 0;
            foreach (var userId in userIds)
                try
                {
                    var plays = await journal.GetPlaysInWindow(userId, mix, e.StartsAt, e.EndsAt,
                        context.CancellationToken);
                    if (plays.Count == 0) continue;

                    await seasonalBests.Write(mix, userId, ScoreJournalEntry.OfficialImportSource,
                        plays.Select(p => new SeasonalBestWriter.Candidate(
                            new RecordedPhoenixScore(p.ChartId, p.Score, p.Plate, p.IsBroken, p.OccurredAt,
                                p.Source, p.Judgements),
                            RaisedExistingRecord: false, p.IsStageBroken)).ToArray(),
                        context.CancellationToken);
                    replayed++;
                }
                catch (Exception ex)
                {
                    // One player's unresolvable history must not abort the season, and leave it
                    // half rebuilt with no way to tell which half.
                    logger.LogError(ex, "Season backfill failed for user {UserId} ({Mix}, {Season})", userId, mix,
                        e.Season);
                }

            logger.LogInformation("Backfilled {Count} of {Total} players on {Mix} for {Season}", replayed,
                userIds.Count, mix, e.Season);
        }

        // The pool exists; nothing has priced it into standings. Progression's rollup is what does
        // that, and the Ledger asks for it directly rather than announcing a fact of its own:
        // PlayerProgress cannot reference back this way without closing a cycle, and one command
        // published once per season beats an event no assembly is allowed to hear.
        await context.Publish(new RollupSeasonStatsCommand(e.Season), context.CancellationToken);
    }
}
