using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.ScoreLedger.Contracts;
using ScoreTracker.ScoreLedger.Contracts.Commands;
using ScoreTracker.ScoreLedger.Contracts.Events;
using ScoreTracker.ScoreLedger.Domain;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ScoreLedger.Application;

/// <summary>
///     Sittings: the plays the plays endpoint records, gathered by play time and announced once each
///     (docs/design/rise.md §12). Recording finds or opens each play's sitting, writes the plays under
///     it and schedules its close; the close replays the sitting from the journal once the quiet
///     window has passed with nothing arriving; the five-minute sweep closes a sitting whose scheduled
///     close was lost. Nothing is held in memory, so a restart costs a sitting nothing but a later card.
/// </summary>
internal sealed class SittingSaga(
    IScoreSessionRepository sessions,
    IMediator mediator,
    IMessageScheduler scheduler,
    IDateTimeOffsetAccessor dateTime,
    SittingGate gate,
    ILogger<SittingSaga> logger)
    : IRequestHandler<RecordSittingPlaysCommand>,
        IConsumer<SittingSaga.CloseSittingCommand>,
        IConsumer<OverdueScoreBatchesFlushedEvent>
{
    // A sweep closes at most this many sittings, oldest first; the next tick takes the rest.
    private const int SweepCap = 25;

    public sealed record CloseSittingCommand(Guid UserId, MixEnum Mix, Guid SessionId);

    public async Task Handle(RecordSittingPlaysCommand request, CancellationToken cancellationToken)
    {
        if (request.Plays.Count == 0) return;
        var now = dateTime.Now;
        using var entered = await gate.Enter(request.UserId, request.Mix, cancellationToken);

        var open = await sessions.GetOpenSittings(request.UserId, request.Mix,
            now - ScoreBatchPolicy.SittingQuietWindow, cancellationToken);
        var plan = SittingPlanner.Plan(open, request.Plays.Select(p => p.PlayedAt).ToArray(),
            ScoreBatchPolicy.SittingQuietWindow);
        foreach (var opened in plan.Opened)
            await sessions.Open(opened.Id, request.UserId, request.Mix, request.Source, null, null, now,
                cancellationToken);

        foreach (var sitting in request.Plays.Select((play, i) => (Play: play, SittingId: plan.SittingIds[i]))
                     .GroupBy(x => x.SittingId, x => x.Play))
        {
            var plays = sitting.ToArray();
            // The record first — keep-best, so only a play that beats it moves it, dated by the play;
            // a break only where the player seats breaks — then the journal, which is idempotent on
            // play time and so never journals a record's play twice.
            foreach (var play in plays.Where(p => !p.IsBroken || request.RecordBrokenAsBest))
                await mediator.Send(new UpdatePhoenixBestAttemptCommand(play.ChartId, play.IsBroken, play.Score,
                    play.Plate, KeepBestStats: true, Source: request.Source, Mix: request.Mix,
                    SessionId: sitting.Key, RecordedAt: play.PlayedAt, Judgements: play.Judgements,
                    DeferAnnouncement: true), cancellationToken);
            await mediator.Send(new RecordObservedPlaysCommand(request.UserId, request.Mix, request.Source,
                sitting.Key, plays, IncludeBroken: request.RecordBrokenAsBest), cancellationToken);

            await sessions.TouchArrival(sitting.Key, now, cancellationToken);
            await scheduler.SchedulePublish(
                now.UtcDateTime + ScoreBatchPolicy.SittingQuietWindow + ScoreBatchPolicy.DrainBuffer,
                new CloseSittingCommand(request.UserId, request.Mix, sitting.Key), cancellationToken);
        }
    }

    public async Task Consume(ConsumeContext<CloseSittingCommand> context)
    {
        var message = context.Message;
        await Close(message.UserId, message.Mix, message.SessionId, context.CancellationToken);
    }

    public async Task Consume(ConsumeContext<OverdueScoreBatchesFlushedEvent> context)
    {
        var overdue = await sessions.ListOverdueSittings(dateTime.Now - ScoreBatchPolicy.SittingOverdueAfter,
            SweepCap, context.CancellationToken);
        foreach (var sitting in overdue)
            try
            {
                await Close(sitting.UserId, sitting.Mix, sitting.Id, context.CancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Closing overdue sitting {SessionId} failed", sitting.Id);
            }
    }

    // Every arrival schedules its own close, so a close that finds a newer arrival leaves the work
    // to the close that arrival scheduled. A sitting already replayed carries its counts and one
    // already announced its processed stamp; either way there is nothing left to announce.
    private async Task Close(Guid userId, MixEnum mix, Guid sessionId, CancellationToken cancellationToken)
    {
        using var entered = await gate.Enter(userId, mix, cancellationToken);
        var sitting = await sessions.Get(sessionId, cancellationToken);
        if (sitting is null || sitting.ProcessedAt is not null) return;
        if (sitting.NewCount > 0 || sitting.UpscoreCount > 0) return;
        if (dateTime.Now - sitting.LastActivityAt < ScoreBatchPolicy.SittingQuietWindow) return;
        // A sitting whose last play is more than a day old by the time it closes is a backlog a tool
        // sent late: it records and captures as usual, and posts no card.
        var lastPlayed = await sessions.GetLastPlayedAt(userId, sessionId, cancellationToken);
        var announce = lastPlayed == null || dateTime.Now - lastPlayed.Value <= ScoreBatchPolicy.SittingCardCutoff;
        await mediator.Send(new ReplaySessionCommand(userId, sessionId, announce), cancellationToken);
    }
}
