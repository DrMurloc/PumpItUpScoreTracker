using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.ScoreLedger.Application;
using ScoreTracker.ScoreLedger.Domain;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.TestHelpers;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class SeasonalBestWriterTests
{
    private static readonly Guid Player = Guid.NewGuid();
    private static readonly Guid Chart = Guid.NewGuid();
    private static readonly SeasonId Summer = SeasonId.From(2026, 3);
    private static readonly SeasonId Fall = SeasonId.From(2026, 4);

    private static readonly DateTimeOffset InSummer = new(2026, 8, 15, 20, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset InFall = new(2026, 11, 15, 20, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset LongAgo = new(2025, 5, 1, 20, 0, 0, TimeSpan.Zero);

    private static SeasonRecord Season(SeasonId id)
    {
        var offset = TimeSpan.FromHours(-5);
        var month = id.Quarter * 3;
        return new SeasonRecord(id, $"Q{id.Quarter} {id.Year}",
            new DateTimeOffset(new DateTime(id.Year, month - 2, 1, 0, 0, 0), offset),
            new DateTimeOffset(new DateTime(id.Year, month, DateTime.DaysInMonth(id.Year, month), 23, 59, 59), offset),
            null, false);
    }

    private static readonly IReadOnlyList<SeasonRecord> Calendar = new[] { Season(Fall), Season(Summer) };

    private static RecordedPhoenixScore Play(int score, DateTimeOffset at)
    {
        return new RecordedPhoenixScore(Chart, PhoenixScore.From(score), PhoenixPlate.SuperbGame, false, at,
            ScoreJournalEntry.OfficialImportSource);
    }

    [Fact]
    public async Task AnInWindowPlayIsWrittenToTheSeasonThatHoldsIt()
    {
        var records = new Mock<IPhoenixRecordRepository>();
        var writer = SeasonalBests.Over(records, Calendar, InFall);

        await writer.Write(MixEnum.Phoenix2, Player, ScoreJournalEntry.OfficialImportSource,
            new[] { new SeasonalBestWriter.Candidate(Play(950_000, InFall), false) }, CancellationToken.None);

        records.Verify(r => r.UpdateBestAttempt(MixEnum.Phoenix2, Player,
            It.Is<RecordedPhoenixScore>(s => s.Score!.Value == 950_000), Fall, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task APlayBelowTheAllTimeBestIsStillTheSeasonsBest()
    {
        // The whole point of the observed-plays path: the season's pool starts empty, so a run the
        // all-time record would have thrown away is this season's best on the chart.
        var records = new Mock<IPhoenixRecordRepository>();
        records.Setup(r => r.GetRecordedScore(MixEnum.Phoenix2, Player, Chart, Fall, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RecordedPhoenixScore?)null);
        var writer = SeasonalBests.Over(records, Calendar, InFall);

        await writer.Write(MixEnum.Phoenix2, Player, ScoreJournalEntry.OfficialImportSource,
            new[] { new SeasonalBestWriter.Candidate(Play(820_000, InFall), false) }, CancellationToken.None);

        records.Verify(r => r.UpdateBestAttempt(MixEnum.Phoenix2, Player, It.IsAny<RecordedPhoenixScore>(), Fall,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AnUpscoreWearingAPreSeasonDateLandsOnTheRunningSeason()
    {
        var records = new Mock<IPhoenixRecordRepository>();
        var writer = SeasonalBests.Over(records, Calendar, InFall);

        await writer.Write(MixEnum.Phoenix2, Player, ScoreJournalEntry.OfficialImportSource,
            new[] { new SeasonalBestWriter.Candidate(Play(990_000, LongAgo), RaisedExistingRecord: true) },
            CancellationToken.None);

        records.Verify(r => r.UpdateBestAttempt(MixEnum.Phoenix2, Player, It.IsAny<RecordedPhoenixScore>(), Fall,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AFirstEverImportersOldCardsWriteNothing()
    {
        var records = new Mock<IPhoenixRecordRepository>();
        var writer = SeasonalBests.Over(records, Calendar, InFall);

        await writer.Write(MixEnum.Phoenix2, Player, ScoreJournalEntry.OfficialImportSource,
            new[] { new SeasonalBestWriter.Candidate(Play(990_000, LongAgo), RaisedExistingRecord: false) },
            CancellationToken.None);

        records.Verify(r => r.UpdateBestAttempt(It.IsAny<MixEnum>(), It.IsAny<Guid>(),
            It.IsAny<RecordedPhoenixScore>(), It.IsAny<SeasonId>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AManualEntryNeverSeedsASeason()
    {
        var records = new Mock<IPhoenixRecordRepository>();
        var writer = SeasonalBests.Over(records, Calendar, InFall);

        await writer.Write(MixEnum.Phoenix2, Player, ScoreJournalEntry.ManualSource,
            new[] { new SeasonalBestWriter.Candidate(Play(950_000, InFall), false) }, CancellationToken.None);

        records.Verify(r => r.UpdateBestAttempt(It.IsAny<MixEnum>(), It.IsAny<Guid>(),
            It.IsAny<RecordedPhoenixScore>(), It.IsAny<SeasonId>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SeveralRunsOfOneChartCostOneReadAndOneWrite()
    {
        // A recent window holds an evening of the same song. Only the best of them is the season's.
        var records = new Mock<IPhoenixRecordRepository>();
        var writer = SeasonalBests.Over(records, Calendar, InFall);

        await writer.Write(MixEnum.Phoenix2, Player, ScoreJournalEntry.OfficialImportSource, new[]
        {
            new SeasonalBestWriter.Candidate(Play(910_000, InFall), false),
            new SeasonalBestWriter.Candidate(Play(940_000, InFall.AddMinutes(4)), false),
            new SeasonalBestWriter.Candidate(Play(925_000, InFall.AddMinutes(8)), false)
        }, CancellationToken.None);

        records.Verify(r => r.GetRecordedScore(MixEnum.Phoenix2, Player, Chart, Fall, It.IsAny<CancellationToken>()),
            Times.Once);
        records.Verify(r => r.UpdateBestAttempt(MixEnum.Phoenix2, Player,
                It.Is<RecordedPhoenixScore>(s => s.Score!.Value == 940_000), Fall, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PlaysOnBothSidesOfABoundaryGoToTheirOwnSeasons()
    {
        // The seven-day grace: two seasons accept writes at once, each from its own plays.
        var records = new Mock<IPhoenixRecordRepository>();
        var writer = SeasonalBests.Over(records, Calendar, InFall);

        await writer.Write(MixEnum.Phoenix2, Player, ScoreJournalEntry.OfficialImportSource, new[]
        {
            new SeasonalBestWriter.Candidate(Play(930_000, InSummer), false),
            new SeasonalBestWriter.Candidate(Play(940_000, InFall), false)
        }, CancellationToken.None);

        records.Verify(r => r.UpdateBestAttempt(MixEnum.Phoenix2, Player,
            It.Is<RecordedPhoenixScore>(s => s.Score!.Value == 930_000), Summer, It.IsAny<CancellationToken>()), Times.Once);
        records.Verify(r => r.UpdateBestAttempt(MixEnum.Phoenix2, Player,
            It.Is<RecordedPhoenixScore>(s => s.Score!.Value == 940_000), Fall, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ARunThatDoesNotBeatTheSeasonsStandingRowIsLeftAlone()
    {
        var records = new Mock<IPhoenixRecordRepository>();
        records.Setup(r => r.GetRecordedScore(MixEnum.Phoenix2, Player, Chart, Fall, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Play(960_000, InFall));
        var writer = SeasonalBests.Over(records, Calendar, InFall);

        await writer.Write(MixEnum.Phoenix2, Player, ScoreJournalEntry.OfficialImportSource,
            new[] { new SeasonalBestWriter.Candidate(Play(940_000, InFall), false) }, CancellationToken.None);

        records.Verify(r => r.UpdateBestAttempt(It.IsAny<MixEnum>(), It.IsAny<Guid>(),
            It.IsAny<RecordedPhoenixScore>(), It.IsAny<SeasonId>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AStageBreakIsNeverASeasonsBest()
    {
        var records = new Mock<IPhoenixRecordRepository>();
        var writer = SeasonalBests.Over(records, Calendar, InFall);

        await writer.Write(MixEnum.Phoenix2, Player, ScoreJournalEntry.OfficialImportSource,
            new[] { new SeasonalBestWriter.Candidate(Play(300_000, InFall), false, IsStageBroken: true) },
            CancellationToken.None);

        records.Verify(r => r.UpdateBestAttempt(It.IsAny<MixEnum>(), It.IsAny<Guid>(),
            It.IsAny<RecordedPhoenixScore>(), It.IsAny<SeasonId>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PhoenixOneNeverGetsASeasonalRowNoMatterWhatItImports()
    {
        // Phoenix 1 has no seasons and never will (§1). The rollup and the backfill both skip it, so
        // a row written here would be an orphan nothing ever recomputes — and Phoenix 1 is the larger
        // importing population of the two.
        var records = new Mock<IPhoenixRecordRepository>();
        var writer = SeasonalBests.Over(records, Calendar, InFall);

        await writer.Write(MixEnum.Phoenix, Player, ScoreJournalEntry.OfficialImportSource,
            new[] { new SeasonalBestWriter.Candidate(Play(950_000, InFall), false) }, CancellationToken.None);

        records.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task BeforeTheFirstRollTheWriterDoesNotEvenReadTheRecordTable()
    {
        var records = new Mock<IPhoenixRecordRepository>();
        var writer = SeasonalBests.Over(records, Array.Empty<SeasonRecord>(), InFall);

        await writer.Write(MixEnum.Phoenix2, Player, ScoreJournalEntry.OfficialImportSource,
            new[] { new SeasonalBestWriter.Candidate(Play(950_000, InFall), false) }, CancellationToken.None);

        records.VerifyNoOtherCalls();
    }
}
