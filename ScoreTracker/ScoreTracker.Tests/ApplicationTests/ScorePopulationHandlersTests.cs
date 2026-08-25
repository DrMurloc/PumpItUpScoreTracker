using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using ScoreTracker.Domain.Records;
using ScoreTracker.ScoreLedger.Application;
using ScoreTracker.ScoreLedger.Contracts;
using ScoreTracker.ScoreLedger.Contracts.Queries;
using ScoreTracker.ScoreLedger.Domain;
using ScoreTracker.SharedKernel.Enums;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class ScorePopulationHandlersTests
{
    private readonly Mock<IScorePopulationRepository> _repository = new();
    private readonly IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions());

    [Fact]
    public async Task PopulationIsReadOncePerMixAndThenServedFromCache()
    {
        var rows = new[] { new LevelScorePopulation(18, 100, 1, 10, 20, 25, 24, 12, 8) };
        _repository.Setup(r => r.GetPopulationByLevel(MixEnum.Phoenix2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(rows);
        var handler = new GetScorePopulationHandler(_repository.Object, _cache);

        var first = await handler.Handle(new GetScorePopulationQuery(MixEnum.Phoenix2), CancellationToken.None);
        var second = await handler.Handle(new GetScorePopulationQuery(MixEnum.Phoenix2), CancellationToken.None);

        Assert.Equal(rows, first);
        Assert.Equal(rows, second);
        _repository.Verify(r => r.GetPopulationByLevel(MixEnum.Phoenix2, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SpreadsBandEachPlayByTheQueriedMixesOwnFloors()
    {
        // 905,000 is an AA on Phoenix and an A+ on Phoenix 2 — the same play must land in a
        // different band per mix, resolved from the score rather than any stored letter.
        var judged = new[] { new JudgedBest(905_000, 950, 30, 10, 5, 5, 400) };
        _repository.Setup(r => r.GetJudgedBests(It.IsAny<MixEnum>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(judged);

        var phoenix = await new GetJudgementSpreadsHandler(_repository.Object, _cache)
            .Handle(new GetJudgementSpreadsQuery(MixEnum.Phoenix), CancellationToken.None);
        var phoenix2 = await new GetJudgementSpreadsHandler(_repository.Object, _cache)
            .Handle(new GetJudgementSpreadsQuery(MixEnum.Phoenix2), CancellationToken.None);

        Assert.Equal(PhoenixLetterGrade.AA, Assert.Single(phoenix).Grade);
        Assert.Equal(PhoenixLetterGrade.APlus, Assert.Single(phoenix2).Grade);
    }

    [Fact]
    public async Task SpreadsScaleEveryPlayToItsOwnThousandNotes()
    {
        // A 500-note play with 10 greats reads 20 per 1,000; combo averages only the row
        // that has one and says how many that was.
        var judged = new[]
        {
            new JudgedBest(985_000, 480, 10, 5, 3, 2, 250),
            new JudgedBest(985_000, 960, 20, 10, 6, 4, null)
        };
        _repository.Setup(r => r.GetJudgedBests(MixEnum.Phoenix2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(judged);
        var handler = new GetJudgementSpreadsHandler(_repository.Object, _cache);

        var spread = Assert.Single(await handler.Handle(new GetJudgementSpreadsQuery(MixEnum.Phoenix2),
            CancellationToken.None));

        Assert.Equal(PhoenixLetterGrade.SSPlus, spread.Grade);
        Assert.Equal(2, spread.Plays);
        Assert.Equal(20.0, spread.GreatsPer1000, 3);
        Assert.Equal(10.0, spread.GoodsPer1000, 3);
        Assert.Equal(500.0, spread.ComboPer1000, 3);
        Assert.Equal(1, spread.CombosMeasured);
    }

    [Fact]
    public async Task SpreadsArriveBestGradeFirst()
    {
        var judged = new[]
        {
            new JudgedBest(910_000, 900, 60, 20, 10, 10, 300),
            new JudgedBest(996_000, 995, 4, 1, 0, 0, 990)
        };
        _repository.Setup(r => r.GetJudgedBests(MixEnum.Phoenix, It.IsAny<CancellationToken>()))
            .ReturnsAsync(judged);
        var handler = new GetJudgementSpreadsHandler(_repository.Object, _cache);

        var spreads = await handler.Handle(new GetJudgementSpreadsQuery(MixEnum.Phoenix),
            CancellationToken.None);

        Assert.Equal(new[] { PhoenixLetterGrade.SSSPlus, PhoenixLetterGrade.AA },
            spreads.Select(s => s.Grade).ToArray());
    }

    [Fact]
    public async Task JudgedPlaysPassThroughWithTheLimitClamped()
    {
        var journal = new Mock<IScoreJournalRepository>();
        var userId = Guid.NewGuid();
        var entries = new[]
        {
            new ScoreJournalEntry(DateTimeOffset.UtcNow, ScoreJournalEntry.OfficialImportSource, userId,
                Guid.NewGuid(), 950_000, PhoenixPlate.FairGame, false, MixEnum.Phoenix2)
        };
        journal.Setup(j => j.GetJudgedPlays(userId, MixEnum.Phoenix2, 500, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entries);
        var handler = new GetJudgedPlaysHandler(journal.Object);

        var result = await handler.Handle(new GetJudgedPlaysQuery(userId, MixEnum.Phoenix2, 9_999),
            CancellationToken.None);

        Assert.Equal(entries, result);
        journal.Verify(j => j.GetJudgedPlays(userId, MixEnum.Phoenix2, 500, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
