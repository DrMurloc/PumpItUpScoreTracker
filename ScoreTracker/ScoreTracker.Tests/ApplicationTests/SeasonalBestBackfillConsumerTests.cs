using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.PlayerProgress.Contracts.Messages;
using ScoreTracker.ScoreLedger.Application;
using ScoreTracker.ScoreLedger.Domain;
using ScoreTracker.Seasons.Contracts.Events;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.TestHelpers;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

/// <summary>
///     The Ledger's half of the backfill (docs/design/seasons.md D23): the journal's in-window plays
///     replayed through the writer a live import uses, then Progression asked to price them.
/// </summary>
public sealed class SeasonalBestBackfillConsumerTests
{
    private static readonly SeasonId Fall = SeasonId.From(2026, 4);
    private static readonly DateTimeOffset Starts = new(2026, 10, 1, 5, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Ends = new(2027, 1, 1, 4, 59, 59, TimeSpan.Zero);
    private static readonly DateTimeOffset InFall = new(2026, 11, 15, 20, 0, 0, TimeSpan.Zero);

    private static readonly Guid Alice = Guid.NewGuid();
    private static readonly Guid Bob = Guid.NewGuid();
    private static readonly Guid Chart = Guid.NewGuid();

    private readonly Mock<IScoreJournalRepository> _journal = new();
    private readonly Mock<IPhoenixRecordRepository> _records = new();

    private SeasonalBestBackfillConsumer Build()
    {
        var writer = SeasonalBests.Over(_records, new[] { FakeSeasons.Quarter(Fall) }, InFall);
        return new SeasonalBestBackfillConsumer(_journal.Object, writer,
            NullLogger<SeasonalBestBackfillConsumer>.Instance);
    }

    private void GivenPlayers(params Guid[] userIds)
    {
        _journal.Setup(j => j.GetUsersWithPlaysInWindow(MixEnum.Phoenix2, Starts, Ends, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userIds);
    }

    private void GivenPlays(Guid userId, params ScoreJournalEntry[] plays)
    {
        _journal.Setup(j => j.GetPlaysInWindow(userId, MixEnum.Phoenix2, Starts, Ends, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plays);
    }

    private static ScoreJournalEntry Play(Guid userId, int score, DateTimeOffset at)
    {
        return new ScoreJournalEntry(at, ScoreJournalEntry.OfficialImportSource, userId, Chart,
            PhoenixScore.From(score), PhoenixPlate.SuperbGame, false, MixEnum.Phoenix2);
    }

    private static Mock<ConsumeContext<SeasonBackfillRequestedEvent>> Context()
    {
        var context = new Mock<ConsumeContext<SeasonBackfillRequestedEvent>>();
        context.SetupGet(c => c.Message).Returns(new SeasonBackfillRequestedEvent(Fall, Starts, Ends));
        context.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        return context;
    }

    [Fact]
    public async Task EachPlayersWindowIsReplayedIntoTheirSeasonalBestAndTheRollupIsAskedFor()
    {
        GivenPlayers(Alice, Bob);
        // Alice played the chart twice; only her better run is the season's best.
        GivenPlays(Alice, Play(Alice, 910_000, InFall), Play(Alice, 940_000, InFall.AddMinutes(5)));
        GivenPlays(Bob, Play(Bob, 970_000, InFall));
        var context = Context();

        await Build().Consume(context.Object);

        _records.Verify(r => r.UpdateBestAttempt(MixEnum.Phoenix2, Alice,
            It.Is<RecordedPhoenixScore>(s => s.Score!.Value == 940_000), Fall, It.IsAny<CancellationToken>()),
            Times.Once);
        _records.Verify(r => r.UpdateBestAttempt(MixEnum.Phoenix2, Bob,
            It.Is<RecordedPhoenixScore>(s => s.Score!.Value == 970_000), Fall, It.IsAny<CancellationToken>()),
            Times.Once);
        // The pool exists; Progression is what turns it into standings.
        context.Verify(c => c.Publish(It.Is<RollupSeasonStatsCommand>(m => m.Season == Fall),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task OnePlayersUnresolvableHistoryDoesNotAbortTheSeason()
    {
        GivenPlayers(Alice, Bob);
        _journal.Setup(j => j.GetPlaysInWindow(Alice, MixEnum.Phoenix2, Starts, Ends, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("unresolvable"));
        GivenPlays(Bob, Play(Bob, 970_000, InFall));
        var context = Context();

        await Build().Consume(context.Object);

        _records.Verify(r => r.UpdateBestAttempt(MixEnum.Phoenix2, Bob, It.IsAny<RecordedPhoenixScore>(), Fall,
            It.IsAny<CancellationToken>()), Times.Once);
        context.Verify(c => c.Publish(It.IsAny<RollupSeasonStatsCommand>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task APlayerWithNothingInTheWindowIsSkipped()
    {
        GivenPlayers(Alice);
        GivenPlays(Alice);

        await Build().Consume(Context().Object);

        _records.Verify(r => r.UpdateBestAttempt(It.IsAny<MixEnum>(), It.IsAny<Guid>(),
            It.IsAny<RecordedPhoenixScore>(), It.IsAny<SeasonId>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PhoenixOneIsNeverBackfilled()
    {
        // Phoenix 1 has no seasons and never will (§1), so the consumer never asks about it.
        GivenPlayers();

        await Build().Consume(Context().Object);

        _journal.Verify(j => j.GetUsersWithPlaysInWindow(MixEnum.Phoenix, It.IsAny<DateTimeOffset>(),
            It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Never);
        _journal.Verify(j => j.GetUsersWithPlaysInWindow(MixEnum.Phoenix2, Starts, Ends,
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
