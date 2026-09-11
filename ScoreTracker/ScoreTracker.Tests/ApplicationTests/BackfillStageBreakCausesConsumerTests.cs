using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.ScoreLedger.Application;
using ScoreTracker.ScoreLedger.Contracts.Messages;
using ScoreTracker.ScoreLedger.Domain;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

/// <summary>
///     The backfill re-solves what the write path would have stored, over what is already there.
///     The arithmetic is StageBreakCauseSolver's and is pinned in its own tests; these pin the
///     walk — who is visited, which rows, what is written back — and that only stage breaks are
///     touched.
/// </summary>
public sealed class BackfillStageBreakCausesConsumerTests
{
    private static readonly Guid Alice = Guid.NewGuid();
    private static readonly Guid Chart = Guid.NewGuid();
    private static readonly DateTimeOffset At = new(2026, 8, 27, 9, 23, 32, TimeSpan.FromHours(9));

    [Fact]
    public async Task AStageBreakTheLifebarCannotExplainIsWrittenBackWithItsCommand()
    {
        // Iolite Sky D21 under Pass SSS+.
        var ctx = new ConsumerContext();
        ctx.GivenChart(MixEnum.Phoenix2, Chart, 1000, 21);
        ctx.GivenStageBreak(MixEnum.Phoenix2, Alice, new JudgementCounts(806, 1, 0, 0, 4));

        await ctx.Consumer.Consume(Context());

        ctx.Journal.Verify(j => j.SetStageBreakCauses(Alice, MixEnum.Phoenix2,
            It.Is<IReadOnlyList<(Guid ChartId, DateTimeOffset OccurredAt, StageBreakCause Cause)>>(c =>
                c.Count == 1 && c[0].ChartId == Chart && c[0].OccurredAt == At &&
                c[0].Cause.IsNonLifebarBreak && c[0].Cause.PassGrade == PhoenixLetterGrade.SSSPlus),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ARunTheLifebarExplainsIsWrittenBackWithNoClaim()
    {
        var ctx = new ConsumerContext();
        ctx.GivenChart(MixEnum.Phoenix2, Chart, 1100, 21);
        ctx.GivenStageBreak(MixEnum.Phoenix2, Alice, new JudgementCounts(451, 5, 2, 1, 61));

        await ctx.Consumer.Consume(Context());

        ctx.Journal.Verify(j => j.SetStageBreakCauses(Alice, MixEnum.Phoenix2,
            It.Is<IReadOnlyList<(Guid ChartId, DateTimeOffset OccurredAt, StageBreakCause Cause)>>(c =>
                c.Count == 1 && !c[0].Cause.IsNonLifebarBreak && !c[0].Cause.IsNamed),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task APlayerWithJudgedRowsButNoStageBreaksWritesNothing()
    {
        // The read is stage breaks only — the repository filters in SQL, so a player whose
        // judged rows are all passes and finished fails costs one empty query and no write.
        var ctx = new ConsumerContext();
        ctx.GivenChart(MixEnum.Phoenix2, Chart, 1000, 21);
        ctx.GivenPlayerWithNoStageBreaks(MixEnum.Phoenix2, Alice);

        await ctx.Consumer.Consume(Context());

        ctx.Journal.Verify(j => j.SetStageBreakCauses(It.IsAny<Guid>(), It.IsAny<MixEnum>(),
            It.IsAny<IReadOnlyList<(Guid, DateTimeOffset, StageBreakCause)>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LegacyMixesAreNeverWalked()
    {
        // A cause is read against a mix's grade floors and its life bar. XX has neither.
        var ctx = new ConsumerContext();

        await ctx.Consumer.Consume(Context());

        ctx.Journal.Verify(j => j.GetUsersWithJudgedEntries(MixEnum.XX, It.IsAny<CancellationToken>()), Times.Never);
        ctx.Journal.Verify(j => j.GetUsersWithJudgedEntries(MixEnum.Phoenix2, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ASessionsReplaysOfAChartAreSolvedTogether()
    {
        // Alone, the six-miss run guesses SSS and matches Marvelous Game; its replay in the same
        // session could only have crossed SSS+ and matches no plate, so both are written back SSS+.
        var ctx = new ConsumerContext();
        var session = Guid.NewGuid();
        ctx.GivenChart(MixEnum.Phoenix2, Chart, 1000, 26);
        ctx.GivenStageBreaks(MixEnum.Phoenix2, Alice,
            (new JudgementCounts(900, 0, 0, 0, 6), session, At),
            (new JudgementCounts(806, 1, 0, 0, 4), session, At.AddMinutes(2)));

        await ctx.Consumer.Consume(Context());

        Assert.Equal(2, ctx.Written.Count);
        Assert.All(ctx.Written, row =>
        {
            Assert.Equal(PhoenixLetterGrade.SSSPlus, row.Cause.PassGrade);
            Assert.Null(row.Cause.PassPlate);
        });
    }

    [Fact]
    public async Task BreaksInDifferentSessionsAreSolvedApart()
    {
        var ctx = new ConsumerContext();
        ctx.GivenChart(MixEnum.Phoenix2, Chart, 1000, 26);
        ctx.GivenStageBreaks(MixEnum.Phoenix2, Alice,
            (new JudgementCounts(900, 0, 0, 0, 6), Guid.NewGuid(), At),
            (new JudgementCounts(806, 1, 0, 0, 4), Guid.NewGuid(), At.AddMinutes(2)));

        await ctx.Consumer.Consume(Context());

        var sixMisses = Assert.Single(ctx.Written, row => row.OccurredAt == At);
        Assert.Equal(PhoenixLetterGrade.SSS, sixMisses.Cause.PassGrade);
        Assert.Equal(PhoenixPlate.MarvelousGame, sixMisses.Cause.PassPlate);
    }

    [Fact]
    public async Task RowsFromBeforeSessionCaptureShareACalendarDay()
    {
        var ctx = new ConsumerContext();
        ctx.GivenChart(MixEnum.Phoenix2, Chart, 1000, 26);
        ctx.GivenStageBreaks(MixEnum.Phoenix2, Alice,
            (new JudgementCounts(900, 0, 0, 0, 6), null, At),
            (new JudgementCounts(806, 1, 0, 0, 4), null, At.AddHours(2)),
            (new JudgementCounts(900, 0, 0, 0, 6), null, At.AddDays(1)));

        await ctx.Consumer.Consume(Context());

        Assert.Equal(PhoenixLetterGrade.SSSPlus,
            Assert.Single(ctx.Written, row => row.OccurredAt == At).Cause.PassGrade);
        Assert.Equal(PhoenixLetterGrade.SSS,
            Assert.Single(ctx.Written, row => row.OccurredAt == At.AddDays(1)).Cause.PassGrade);
    }

    private static ConsumeContext<BackfillStageBreakCausesCommand> Context()
    {
        var context = new Mock<ConsumeContext<BackfillStageBreakCausesCommand>>();
        context.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        return context.Object;
    }

    private sealed class ConsumerContext
    {
        public Mock<IScoreJournalRepository> Journal { get; } = new();
        public Mock<IChartRepository> Charts { get; } = new();
        public List<(Guid ChartId, DateTimeOffset OccurredAt, StageBreakCause Cause)> Written { get; } = new();
        public BackfillStageBreakCausesConsumer Consumer { get; }

        public ConsumerContext()
        {
            Journal.Setup(j => j.GetUsersWithJudgedEntries(It.IsAny<MixEnum>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<Guid>());
            Journal.Setup(j => j.GetJudgedStageBreaks(It.IsAny<Guid>(), It.IsAny<MixEnum>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<ScoreJournalEntry>());
            Journal.Setup(j => j.SetStageBreakCauses(It.IsAny<Guid>(), It.IsAny<MixEnum>(),
                    It.IsAny<IReadOnlyList<(Guid ChartId, DateTimeOffset OccurredAt, StageBreakCause Cause)>>(),
                    It.IsAny<CancellationToken>()))
                .Callback<Guid, MixEnum, IReadOnlyList<(Guid ChartId, DateTimeOffset OccurredAt, StageBreakCause Cause)>,
                    CancellationToken>((_, _, causes, _) => Written.AddRange(causes))
                .Returns(Task.CompletedTask);
            Consumer = new BackfillStageBreakCausesConsumer(Journal.Object, Charts.Object,
                NullLogger<BackfillStageBreakCausesConsumer>.Instance);
        }

        public void GivenStageBreaks(MixEnum mix, Guid userId,
            params (JudgementCounts Judgements, Guid? SessionId, DateTimeOffset At)[] rows)
        {
            Journal.Setup(j => j.GetUsersWithJudgedEntries(mix, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { userId });
            Journal.Setup(j => j.GetJudgedStageBreaks(userId, mix, It.IsAny<CancellationToken>()))
                .ReturnsAsync(rows.Select(row => new ScoreJournalEntry(row.At, ScoreJournalEntry.OfficialImportSource,
                    userId, Chart, null, null, true, mix, row.SessionId, row.Judgements, false,
                    IsStageBroken: true)).ToArray());
        }

        public void GivenChart(MixEnum mix, Guid chartId, int noteCount, int level)
        {
            Charts.Setup(c => c.GetChart(mix, chartId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ChartBuilder().WithId(chartId).WithNoteCount(noteCount)
                    .WithLevel(level).Build());
        }

        public void GivenStageBreak(MixEnum mix, Guid userId, JudgementCounts judgements)
        {
            Journal.Setup(j => j.GetUsersWithJudgedEntries(mix, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { userId });
            Journal.Setup(j => j.GetJudgedStageBreaks(userId, mix, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[]
                {
                    new ScoreJournalEntry(At, ScoreJournalEntry.OfficialImportSource, userId, Chart,
                        null, null, true, mix, null, judgements, false, IsStageBroken: true)
                });
        }

        public void GivenPlayerWithNoStageBreaks(MixEnum mix, Guid userId)
        {
            Journal.Setup(j => j.GetUsersWithJudgedEntries(mix, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { userId });
        }
    }
}
