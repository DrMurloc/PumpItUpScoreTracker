using System;
using System.Collections.Generic;
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
    public async Task AJudgedPlayThatFinishedIsNeverTouched()
    {
        var ctx = new ConsumerContext();
        ctx.GivenChart(MixEnum.Phoenix2, Chart, 1000, 21);
        ctx.GivenFinishedPlay(MixEnum.Phoenix2, Alice, new JudgementCounts(990, 6, 0, 0, 4));

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
        public BackfillStageBreakCausesConsumer Consumer { get; }

        public ConsumerContext()
        {
            Journal.Setup(j => j.GetUsersWithJudgedEntries(It.IsAny<MixEnum>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<Guid>());
            Journal.Setup(j => j.GetJudgedEntries(It.IsAny<Guid>(), It.IsAny<MixEnum>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<ScoreJournalEntry>());
            Consumer = new BackfillStageBreakCausesConsumer(Journal.Object, Charts.Object,
                NullLogger<BackfillStageBreakCausesConsumer>.Instance);
        }

        public void GivenChart(MixEnum mix, Guid chartId, int noteCount, int level)
        {
            Charts.Setup(c => c.GetChart(mix, chartId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ChartBuilder().WithId(chartId).WithNoteCount(noteCount)
                    .WithLevel(level).Build());
        }

        public void GivenStageBreak(MixEnum mix, Guid userId, JudgementCounts judgements)
        {
            GivenEntry(mix, userId, judgements, true);
        }

        public void GivenFinishedPlay(MixEnum mix, Guid userId, JudgementCounts judgements)
        {
            GivenEntry(mix, userId, judgements, false);
        }

        private void GivenEntry(MixEnum mix, Guid userId, JudgementCounts judgements, bool isStageBroken)
        {
            Journal.Setup(j => j.GetUsersWithJudgedEntries(mix, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { userId });
            Journal.Setup(j => j.GetJudgedEntries(userId, mix, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[]
                {
                    new ScoreJournalEntry(At, ScoreJournalEntry.OfficialImportSource, userId, Chart,
                        null, null, true, mix, null, judgements, false, IsStageBroken: isStageBroken)
                });
        }
    }
}
