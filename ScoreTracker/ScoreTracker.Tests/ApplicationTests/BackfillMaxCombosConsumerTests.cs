using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.ScoreLedger.Application;
using ScoreTracker.ScoreLedger.Contracts.Messages;
using ScoreTracker.ScoreLedger.Domain;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

/// <summary>
///     The backfill re-solves what the write path would have stored, over what is already there.
///     The arithmetic is PhoenixComboSolver's and is pinned in its own tests; these pin the walk —
///     who is visited, which rows, what is written back — and that a corrected note count changes
///     the answer on the next press.
/// </summary>
public sealed class BackfillMaxCombosConsumerTests
{
    private static readonly Guid Alice = Guid.NewGuid();
    private static readonly Guid Bob = Guid.NewGuid();
    private static readonly Guid Chart = Guid.NewGuid();
    private static readonly DateTimeOffset At = new(2026, 8, 1, 12, 0, 0, TimeSpan.Zero);

    // 900/40/5/2/3 at combo 947 on a 950-note chart.
    private static readonly JudgementCounts Counts = new(900, 40, 5, 2, 3);
    private static readonly PhoenixScore Score = new ScoreScreen(900, 40, 5, 2, 3, 947).CalculatePhoenixScore;

    [Fact]
    public async Task EveryPlayerWithAJudgedRowInEitherStoreIsVisitedAndBothStoresAreWritten()
    {
        var ctx = new ConsumerContext();
        ctx.GivenNoteCount(MixEnum.Phoenix, Chart, 950);
        ctx.GivenJudgedRecord(MixEnum.Phoenix, Alice);
        ctx.GivenJudgedEntry(MixEnum.Phoenix, Bob);

        await ctx.Consumer.Consume(Context());

        ctx.Records.Verify(r => r.SetMaxCombos(MixEnum.Phoenix, Alice,
            It.Is<IReadOnlyList<(Guid ChartId, int? MaxCombo)>>(c => c.Count == 1 && c[0].ChartId == Chart && c[0].MaxCombo == 947),
            It.IsAny<CancellationToken>()), Times.Once);
        ctx.Journal.Verify(j => j.SetMaxCombos(Bob, MixEnum.Phoenix,
            It.Is<IReadOnlyList<(Guid ChartId, DateTimeOffset OccurredAt, int? MaxCombo)>>(c =>
                c.Count == 1 && c[0].ChartId == Chart && c[0].OccurredAt == At && c[0].MaxCombo == 947),
            It.IsAny<CancellationToken>()), Times.Once);
        // Alice has no journal rows and Bob no records: the other store gets an empty write, not a skip.
        ctx.Journal.Verify(j => j.SetMaxCombos(Alice, MixEnum.Phoenix,
            It.Is<IReadOnlyList<(Guid ChartId, DateTimeOffset OccurredAt, int? MaxCombo)>>(c => c.Count == 0),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ACorrectedNoteCountChangesTheAnswerOnTheNextPress()
    {
        // The row's stored combo is irrelevant: everything is re-derived from the catalog as it
        // stands now. Against a stale count the play does not cover the chart and the answer is
        // null; once the count is fixed the same rows solve.
        var ctx = new ConsumerContext();
        ctx.GivenJudgedRecord(MixEnum.Phoenix, Alice, storedCombo: 947);
        ctx.GivenNoteCount(MixEnum.Phoenix, Chart, 1000);

        await ctx.Consumer.Consume(Context());
        ctx.Records.Verify(r => r.SetMaxCombos(MixEnum.Phoenix, Alice,
            It.Is<IReadOnlyList<(Guid ChartId, int? MaxCombo)>>(c => c[0].MaxCombo == null),
            It.IsAny<CancellationToken>()), Times.Once);

        ctx.GivenNoteCount(MixEnum.Phoenix, Chart, 950);
        await ctx.Consumer.Consume(Context());
        ctx.Records.Verify(r => r.SetMaxCombos(MixEnum.Phoenix, Alice,
            It.Is<IReadOnlyList<(Guid ChartId, int? MaxCombo)>>(c => c[0].MaxCombo == 947),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AStageBreakRowSolvesToNothingAndUnjudgedRowsAreNeverRead()
    {
        var ctx = new ConsumerContext();
        ctx.GivenNoteCount(MixEnum.Phoenix2, Chart, 1163);
        ctx.Journal.Setup(j => j.GetUsersWithJudgedEntries(MixEnum.Phoenix2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Bob });
        ctx.Journal.Setup(j => j.GetJudgedEntries(Bob, MixEnum.Phoenix2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new ScoreJournalEntry(At, ScoreJournalEntry.OfficialImportSource, Bob, Chart, null, null, true,
                    MixEnum.Phoenix2, null, new JudgementCounts(134, 2, 0, 0, 70), false, IsStageBroken: true)
            });

        await ctx.Consumer.Consume(Context());

        ctx.Journal.Verify(j => j.SetMaxCombos(Bob, MixEnum.Phoenix2,
            It.Is<IReadOnlyList<(Guid ChartId, DateTimeOffset OccurredAt, int? MaxCombo)>>(c =>
                c.Count == 1 && c[0].MaxCombo == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TheCatalogIsAskedOncePerChartPerPlayer()
    {
        var ctx = new ConsumerContext();
        ctx.GivenNoteCount(MixEnum.Phoenix, Chart, 950);
        ctx.GivenJudgedRecord(MixEnum.Phoenix, Alice);
        ctx.GivenJudgedEntry(MixEnum.Phoenix, Alice);

        await ctx.Consumer.Consume(Context());

        ctx.Charts.Verify(c => c.GetChart(MixEnum.Phoenix, Chart, It.IsAny<CancellationToken>()), Times.Once);
    }

    private static ConsumeContext<BackfillMaxCombosCommand> Context()
    {
        var ctx = new Mock<ConsumeContext<BackfillMaxCombosCommand>>();
        ctx.SetupGet(c => c.Message).Returns(new BackfillMaxCombosCommand());
        ctx.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        return ctx.Object;
    }

    private sealed class ConsumerContext
    {
        public Mock<IPhoenixRecordRepository> Records { get; } = new();
        public Mock<IScoreJournalRepository> Journal { get; } = new();
        public Mock<IChartRepository> Charts { get; } = new();
        public BackfillMaxCombosConsumer Consumer { get; }

        private readonly Dictionary<MixEnum, List<Guid>> _recordUsers = new();
        private readonly Dictionary<MixEnum, List<Guid>> _journalUsers = new();

        public ConsumerContext()
        {
            Records.Setup(r => r.GetUsersWithJudgedRecords(It.IsAny<MixEnum>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((MixEnum mix, CancellationToken _) =>
                    _recordUsers.GetValueOrDefault(mix, new List<Guid>()).ToArray());
            Journal.Setup(j => j.GetUsersWithJudgedEntries(It.IsAny<MixEnum>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((MixEnum mix, CancellationToken _) =>
                    _journalUsers.GetValueOrDefault(mix, new List<Guid>()).ToArray());
            Records.Setup(r => r.GetRecordedScores(It.IsAny<MixEnum>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<RecordedPhoenixScore>());
            Journal.Setup(j => j.GetJudgedEntries(It.IsAny<Guid>(), It.IsAny<MixEnum>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<ScoreJournalEntry>());
            Consumer = new BackfillMaxCombosConsumer(Records.Object, Journal.Object, Charts.Object,
                NullLogger<BackfillMaxCombosConsumer>.Instance);
        }

        public void GivenNoteCount(MixEnum mix, Guid chartId, int noteCount)
        {
            Charts.Setup(c => c.GetChart(mix, chartId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ChartBuilder().WithId(chartId).WithNoteCount(noteCount).Build());
        }

        public void GivenJudgedRecord(MixEnum mix, Guid userId, int? storedCombo = null)
        {
            _recordUsers.TryAdd(mix, new List<Guid>());
            _recordUsers[mix].Add(userId);
            Records.Setup(r => r.GetRecordedScores(mix, userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[]
                {
                    new RecordedPhoenixScore(Chart, Score, PhoenixPlate.FairGame, false, At,
                        ScoreJournalEntry.OfficialImportSource, Counts with { MaxCombo = storedCombo }),
                    // An unjudged record is not the backfill's business.
                    new RecordedPhoenixScore(Guid.NewGuid(), 900000, PhoenixPlate.RoughGame, false, At)
                });
        }

        public void GivenJudgedEntry(MixEnum mix, Guid userId)
        {
            _journalUsers.TryAdd(mix, new List<Guid>());
            _journalUsers[mix].Add(userId);
            Journal.Setup(j => j.GetJudgedEntries(userId, mix, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[]
                {
                    new ScoreJournalEntry(At, ScoreJournalEntry.OfficialImportSource, userId, Chart, Score,
                        PhoenixPlate.FairGame, false, mix, null, Counts, false)
                });
        }
    }
}
