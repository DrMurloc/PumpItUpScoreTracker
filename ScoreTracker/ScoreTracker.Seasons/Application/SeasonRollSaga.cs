using MassTransit;
using Microsoft.Extensions.Logging;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Seasons.Contracts.Events;
using ScoreTracker.Seasons.Contracts.Messages;
using ScoreTracker.Seasons.Domain;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Seasons.Application;

/// <summary>
///     The roll (docs/design/seasons.md §7, D13): stateless like March of Murlocs' scheduler. "Does
///     the quarter I stand in have its row?" — create it; then every ended season past its seven
///     days and not yet sealed gets its stamp. Each step is idempotent on its own, so a crash between
///     them resumes on the next tick, and Roll now on the console is the same message.
/// </summary>
internal sealed class SeasonRollSaga(ISeasonRepository seasons, IDateTimeOffsetAccessor dateTime,
    ILogger<SeasonRollSaga> logger) : IConsumer<RollSeasonCommand>
{
    public async Task Consume(ConsumeContext<RollSeasonCommand> context)
    {
        var now = dateTime.Now;
        var cancellationToken = context.CancellationToken;
        var existing = await seasons.GetAll(cancellationToken);

        var running = SeasonCalendar.QuarterAt(now);
        if (existing.All(s => s.Id != running))
        {
            var opened = Open(running);
            await seasons.Add(opened, cancellationToken);
            logger.LogInformation("Season {Season} ({Name}) opened", opened.Id, opened.Name);
            await context.Publish(new SeasonOpenedEvent(opened.Id, opened.StartsAt, opened.EndsAt), cancellationToken);
        }

        foreach (var ended in existing.Where(s => !s.IsSealed && SeasonCalendar.IsPastGrace(s.Id, now)))
        {
            await seasons.Seal(ended.Id, now, cancellationToken);
            logger.LogInformation("Season {Season} ({Name}) sealed", ended.Id, ended.Name);
            await context.Publish(new SeasonSealedEvent(ended.Id, now), cancellationToken);
        }
    }

    /// <summary>
    ///     A quarter's row as the roll writes it. Never balanced here (D3): a season running when
    ///     balancing ships stays flat, and the roll that computes ratings (slice 4) is what marks a
    ///     later quarter balanced.
    /// </summary>
    internal static SeasonRecord Open(SeasonId season)
    {
        return new SeasonRecord(season, SeasonCalendar.NameOf(season), SeasonCalendar.StartOf(season),
            SeasonCalendar.EndOf(season), null, false);
    }
}
