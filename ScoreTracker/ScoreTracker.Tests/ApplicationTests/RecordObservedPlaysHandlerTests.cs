using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.ScoreLedger.Application;
using ScoreTracker.ScoreLedger.Contracts.Commands;
using ScoreTracker.ScoreLedger.Domain;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

/// <summary>
///     The observation path: what the recent window and the best list's stage breaks become in
///     the journal. Combo and the note-count tripwire are the same code the record path uses,
///     pinned here for the shape they take on an observation.
/// </summary>
public sealed class RecordObservedPlaysHandlerTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid ChartId = Guid.NewGuid();
    private static readonly DateTimeOffset PlayedAt = new(2026, 8, 15, 2, 17, 51, TimeSpan.FromHours(9));

    [Fact]
    public async Task AStageBreakIsJournaledBrokenScorelessFlaggedAndNeverBest()
    {
        var ctx = new HandlerContext();
        var judgements = new JudgementCounts(244, 5, 2, 1, 110);

        await ctx.Handler.Handle(Command(new RecordObservedPlaysCommand.ObservedPlay(ChartId, null, null, false,
            PlayedAt, judgements, IsStageBroken: true)), CancellationToken.None);

        var entry = Assert.Single(ctx.Written);
        Assert.True(entry.IsStageBroken);
        Assert.True(entry.IsBroken);
        Assert.False(entry.IsBest);
        Assert.Null(entry.Score);
        Assert.Null(entry.Plate);
        Assert.Equal(PlayedAt, entry.OccurredAt);
        Assert.Equal(judgements, entry.Judgements);
    }

    [Fact]
    public async Task ABestListStageBreakArrivesWithNoBreakdownAndIsKeptAsAPlay()
    {
        // The Phoenix 2 best list holds a stage break as a chart's first attempt: a date, no
        // judgement table, a running number we do not keep. What survives is that the play
        // happened.
        var ctx = new HandlerContext();

        await ctx.Handler.Handle(Command(new RecordObservedPlaysCommand.ObservedPlay(ChartId, null, null, true,
            PlayedAt, null, IsStageBroken: true)), CancellationToken.None);

        var entry = Assert.Single(ctx.Written);
        Assert.True(entry.IsStageBroken);
        Assert.Null(entry.Judgements);
        Assert.Null(entry.Score);
    }

    [Fact]
    public async Task AStageBreakWithNothingHitIsAWalkOffAndIsNotWritten()
    {
        var ctx = new HandlerContext();

        await ctx.Handler.Handle(Command(new RecordObservedPlaysCommand.ObservedPlay(ChartId, null, null, true,
            PlayedAt, new JudgementCounts(0, 0, 0, 0, 51), IsStageBroken: true)), CancellationToken.None);

        ctx.Journal.Verify(j => j.AppendObservations(It.IsAny<IReadOnlyList<ScoreJournalEntry>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AFinishedFailKeepsItsScoreAndGetsNoPlate()
    {
        var ctx = new HandlerContext();

        await ctx.Handler.Handle(Command(new RecordObservedPlaysCommand.ObservedPlay(ChartId, 590032,
            PhoenixPlate.RoughGame, true, PlayedAt, new JudgementCounts(168, 79, 145, 19, 5))), CancellationToken.None);

        var entry = Assert.Single(ctx.Written);
        Assert.False(entry.IsStageBroken);
        Assert.True(entry.IsBroken);
        Assert.Equal((PhoenixScore)590032, entry.Score);
        Assert.Null(entry.Plate);
    }

    [Fact]
    public async Task TheComboIsSolvedAgainstTheCatalogOncePerChart()
    {
        var ctx = new HandlerContext();
        var counts = new JudgementCounts(900, 40, 5, 2, 3);
        var screen = new ScoreScreen(900, 40, 5, 2, 3, 947);
        ctx.GivenNoteCount(ChartId, counts.NoteCount);

        await ctx.Handler.Handle(Command(
            new RecordObservedPlaysCommand.ObservedPlay(ChartId, screen.CalculatePhoenixScore, PhoenixPlate.FairGame,
                false, PlayedAt, counts),
            new RecordObservedPlaysCommand.ObservedPlay(ChartId, screen.CalculatePhoenixScore, PhoenixPlate.FairGame,
                false, PlayedAt.AddMinutes(4), counts)), CancellationToken.None);

        Assert.Equal(2, ctx.Written.Count);
        Assert.All(ctx.Written, e => Assert.Equal(947, e.Judgements!.MaxCombo));
        ctx.Charts.Verify(c => c.GetChart(MixEnum.Phoenix2, ChartId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ADisagreementWithTheCatalogLogsOnceAndChangesNothing()
    {
        var ctx = new HandlerContext();
        var counts = new JudgementCounts(900, 40, 5, 2, 3);
        ctx.GivenNoteCount(ChartId, 1000);

        await ctx.Handler.Handle(Command(new RecordObservedPlaysCommand.ObservedPlay(ChartId, 985000,
            PhoenixPlate.FairGame, false, PlayedAt, counts)), CancellationToken.None);

        var entry = Assert.Single(ctx.Written);
        Assert.False(entry.IsBroken);
        Assert.Equal((PhoenixScore)985000, entry.Score);
        Assert.Null(entry.Judgements!.MaxCombo);
        ctx.Logger.Verify(l => l.Log(LogLevel.Warning, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception?>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }

    [Fact]
    public async Task ADriftedChartIsNamedOncePerImportNotOncePerPlay()
    {
        // A window holds several runs of the same song; the drift is one fact about the chart.
        var ctx = new HandlerContext();
        var counts = new JudgementCounts(900, 40, 5, 2, 3);
        ctx.GivenNoteCount(ChartId, 1000);

        await ctx.Handler.Handle(Command(
            new RecordObservedPlaysCommand.ObservedPlay(ChartId, 985000, PhoenixPlate.FairGame, false, PlayedAt,
                counts),
            new RecordObservedPlaysCommand.ObservedPlay(ChartId, 980000, PhoenixPlate.FairGame, false,
                PlayedAt.AddMinutes(4), counts)), CancellationToken.None);

        Assert.Equal(2, ctx.Written.Count);
        ctx.Logger.Verify(l => l.Log(LogLevel.Warning, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception?>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }

    [Fact]
    public async Task AStageBreakIsNotADisagreementWhateverItJudged()
    {
        var ctx = new HandlerContext();
        ctx.GivenNoteCount(ChartId, 1163);

        await ctx.Handler.Handle(Command(new RecordObservedPlaysCommand.ObservedPlay(ChartId, null, null, true,
            PlayedAt, new JudgementCounts(134, 2, 0, 0, 70), IsStageBroken: true)), CancellationToken.None);

        ctx.Logger.Verify(l => l.Log(LogLevel.Warning, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception?>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Never);
    }

    private static RecordObservedPlaysCommand Command(params RecordObservedPlaysCommand.ObservedPlay[] plays)
    {
        return new RecordObservedPlaysCommand(UserId, MixEnum.Phoenix2, ScoreJournalEntry.OfficialImportSource,
            Guid.NewGuid(), plays);
    }

    [Fact]
    public async Task AStageBreakTheLifebarCannotExplainCarriesItsPassCommand()
    {
        // Iolite Sky D21, the owner's own Pass SSS+ run: 806 perfects, one great, four misses,
        // 81% of the way in. The bar was never close to empty.
        var ctx = new HandlerContext();
        ctx.GivenChart(ChartId, 1000, 21);

        await ctx.Handler.Handle(Command(new RecordObservedPlaysCommand.ObservedPlay(ChartId, null, null, false,
            PlayedAt, new JudgementCounts(806, 1, 0, 0, 4), IsStageBroken: true)), CancellationToken.None);

        var entry = Assert.Single(ctx.Written);
        Assert.True(entry.Cause.IsNonLifebarBreak);
        Assert.Equal(PhoenixLetterGrade.SSSPlus, entry.Cause.PassGrade);
    }

    [Fact]
    public async Task AStageBreakTheLifebarExplainsMakesNoClaim()
    {
        var ctx = new HandlerContext();
        ctx.GivenChart(ChartId, 1100, 21);

        await ctx.Handler.Handle(Command(new RecordObservedPlaysCommand.ObservedPlay(ChartId, null, null, false,
            PlayedAt, new JudgementCounts(451, 5, 2, 1, 61), IsStageBroken: true)), CancellationToken.None);

        var entry = Assert.Single(ctx.Written);
        Assert.False(entry.Cause.IsNonLifebarBreak);
        Assert.Null(entry.Cause.PassPlate);
        Assert.Null(entry.Cause.PassGrade);
    }

    [Fact]
    public async Task AFinishedPlayNeverCarriesAStageBreakCause()
    {
        var ctx = new HandlerContext();
        ctx.GivenChart(ChartId, 1000, 21);

        await ctx.Handler.Handle(Command(new RecordObservedPlaysCommand.ObservedPlay(ChartId,
            PhoenixScore.From(994_000), PhoenixPlate.MarvelousGame, false,
            PlayedAt, new JudgementCounts(990, 6, 0, 0, 4))), CancellationToken.None);

        var entry = Assert.Single(ctx.Written);
        Assert.False(entry.Cause.IsNonLifebarBreak);
        Assert.False(entry.Cause.IsNamed);
    }

    private sealed class HandlerContext
    {
        public Mock<IScoreJournalRepository> Journal { get; } = new();
        public Mock<IChartRepository> Charts { get; } = new();
        public Mock<ILogger<RecordObservedPlaysHandler>> Logger { get; } = new();
        public List<ScoreJournalEntry> Written { get; } = new();
        public RecordObservedPlaysHandler Handler { get; }

        public HandlerContext()
        {
            Journal.Setup(j => j.AppendObservations(It.IsAny<IReadOnlyList<ScoreJournalEntry>>(),
                    It.IsAny<CancellationToken>()))
                .Callback<IReadOnlyList<ScoreJournalEntry>, CancellationToken>((entries, _) =>
                    Written.AddRange(entries))
                .Returns(Task.CompletedTask);
            Handler = new RecordObservedPlaysHandler(Journal.Object, new MemoryCache(new MemoryCacheOptions()),
                Charts.Object, Logger.Object);
        }

        public void GivenNoteCount(Guid chartId, int noteCount)
        {
            GivenChart(chartId, noteCount, 21);
        }

        public void GivenChart(Guid chartId, int noteCount, int level)
        {
            Charts.Setup(c => c.GetChart(MixEnum.Phoenix2, chartId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ChartBuilder().WithId(chartId).WithNoteCount(noteCount)
                    .WithLevel(level).Build());
        }
    }
}
