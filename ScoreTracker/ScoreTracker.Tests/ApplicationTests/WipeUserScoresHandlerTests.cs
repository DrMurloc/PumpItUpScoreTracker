using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using Moq;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.ScoreLedger.Application;
using ScoreTracker.ScoreLedger.Contracts;
using ScoreTracker.ScoreLedger.Contracts.Commands;
using ScoreTracker.ScoreLedger.Domain;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Tests.TestHelpers;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class WipeUserScoresHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 31, 12, 0, 0, TimeSpan.Zero);

    private static (WipeUserScoresHandler Handler, Context Ctx) Build()
    {
        var ctx = new Context();
        return (new WipeUserScoresHandler(ctx.PhoenixScores.Object, ctx.XxScores.Object, ctx.PlayerStats.Object,
            ctx.Titles.Object, ctx.Journal.Object, ctx.Bus.Object, FakeDateTime.At(Now).Object), ctx);
    }

    [Fact]
    public async Task DeletingBestScoresAloneLeavesTheJournalStanding()
    {
        var (handler, ctx) = Build();
        var userId = Guid.NewGuid();

        await handler.Handle(new WipeUserScoresCommand(userId, null, ScoreDeletionItems.BestScores),
            CancellationToken.None);

        ctx.PhoenixScores.Verify(p => p.DeleteAllForUser(userId, null, It.IsAny<CancellationToken>()), Times.Once);
        ctx.XxScores.Verify(p => p.DeleteAllForUser(userId, null, It.IsAny<CancellationToken>()), Times.Once);
        ctx.Journal.Verify(j => j.DeleteForUser(It.IsAny<Guid>(), It.IsAny<MixEnum?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PlayHistoryDeletesTheJournal()
    {
        // The journal used to survive every wipe, which made "delete my scores" untrue: the
        // plays were still there, chart by chart, and rebuildable into the records.
        var (handler, ctx) = Build();
        var userId = Guid.NewGuid();

        await handler.Handle(new WipeUserScoresCommand(userId, null, ScoreDeletionItems.PlayHistory),
            CancellationToken.None);

        ctx.Journal.Verify(j => j.DeleteForUser(userId, null, It.IsAny<CancellationToken>()), Times.Once);
        ctx.PhoenixScores.Verify(p => p.DeleteAllForUser(It.IsAny<Guid>(), It.IsAny<MixEnum?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AMixScopedWipeOnlyResetsThatMix()
    {
        var (handler, ctx) = Build();
        var userId = Guid.NewGuid();

        await handler.Handle(new WipeUserScoresCommand(userId, MixEnum.Phoenix2, ScoreDeletionItems.BestScores),
            CancellationToken.None);

        ctx.PhoenixScores.Verify(p => p.DeleteAllForUser(userId, MixEnum.Phoenix2, It.IsAny<CancellationToken>()),
            Times.Once);
        ctx.PlayerStats.Verify(p => p.DeleteStats(MixEnum.Phoenix2, userId, It.IsAny<CancellationToken>()),
            Times.Once);
        ctx.PlayerStats.Verify(p => p.DeleteStats(MixEnum.Phoenix, userId, It.IsAny<CancellationToken>()),
            Times.Never);
        ctx.Titles.Verify(t => t.DeleteHighestTitle(MixEnum.Phoenix, userId, It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task EveryMixResetsBothParallelPipelines()
    {
        var (handler, ctx) = Build();
        var userId = Guid.NewGuid();

        await handler.Handle(new WipeUserScoresCommand(userId, null, ScoreDeletionItems.BestScores),
            CancellationToken.None);

        foreach (var mix in new[] { MixEnum.Phoenix, MixEnum.Phoenix2 })
        {
            ctx.PlayerStats.Verify(p => p.DeleteStats(mix, userId, It.IsAny<CancellationToken>()), Times.Once);
            ctx.Titles.Verify(t => t.DeleteHighestTitle(mix, userId, It.IsAny<CancellationToken>()), Times.Once);
            ctx.Bus.Verify(b => b.Publish(
                It.Is<PlayerScoresUpdatedEvent>(e => e.UserId == userId && e.Mix == mix && !e.Changes.Any()),
                It.IsAny<CancellationToken>()), Times.Once);
        }
    }

    [Fact]
    public async Task ProgressionStoresAreToldOnlyWhenSomethingOfTheirsWasChosen()
    {
        // Rating history, highlights and milestones are PlayerProgress's, and none of them are
        // recomputed — deleting the scores behind a milestone strands it rather than clearing it.
        var (handler, ctx) = Build();
        var userId = Guid.NewGuid();

        await handler.Handle(new WipeUserScoresCommand(userId, MixEnum.Phoenix,
            ScoreDeletionItems.Milestones | ScoreDeletionItems.Highlights), CancellationToken.None);

        ctx.Bus.Verify(b => b.Publish(It.Is<PlayerScoreDataDeletedEvent>(e =>
            e.UserId == userId && e.Mix == MixEnum.Phoenix && e.Milestones && e.Highlights &&
            !e.RatingHistory), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ScoreOnlyWipesDoNotDisturbProgressionStores()
    {
        var (handler, ctx) = Build();

        await handler.Handle(new WipeUserScoresCommand(Guid.NewGuid(), null, ScoreDeletionItems.BestScores),
            CancellationToken.None);

        ctx.Bus.Verify(b => b.Publish(It.IsAny<PlayerScoreDataDeletedEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ChoosingNothingDoesNothing()
    {
        var (handler, ctx) = Build();

        await handler.Handle(new WipeUserScoresCommand(Guid.NewGuid(), null, ScoreDeletionItems.None),
            CancellationToken.None);

        ctx.PhoenixScores.VerifyNoOtherCalls();
        ctx.Journal.VerifyNoOtherCalls();
        ctx.Bus.VerifyNoOtherCalls();
    }

    private sealed class Context
    {
        public Mock<IPhoenixRecordRepository> PhoenixScores { get; } = new();
        public Mock<IXXChartAttemptRepository> XxScores { get; } = new();
        public Mock<IPlayerStatsRepository> PlayerStats { get; } = new();
        public Mock<ITitleRepository> Titles { get; } = new();
        public Mock<IScoreJournalRepository> Journal { get; } = new();
        public Mock<IBus> Bus { get; } = new();
    }
}
