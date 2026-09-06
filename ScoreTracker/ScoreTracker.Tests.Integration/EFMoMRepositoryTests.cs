using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.EventCompetition.Domain;
using ScoreTracker.EventCompetition.Infrastructure;
using ScoreTracker.EventCompetition.Infrastructure.Entities;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.Integration.Fixtures;
using ChartType = ScoreTracker.SharedKernel.Enums.ChartType;

namespace ScoreTracker.Tests.Integration;

/// <summary>
///     The season cycle's write side against a real migrated database: a season created with
///     its boards, and the heal of D43 seating the boards a live season is missing, with their
///     snapshot rows beside the ones already held (docs/design/march-of-murlocs.md §12.6).
/// </summary>
[Collection(IntegrationTestCollection.Name)]
[ExcludeFromCodeCoverage]
public sealed class EFMoMRepositoryTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Start = new(2026, 7, 1, 5, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset End = new(2026, 9, 30, 23, 59, 59, TimeSpan.FromHours(-5));
    private readonly SqlServerFixture _fixture;

    public EFMoMRepositoryTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private EFMoMRepository Repo() => new(_fixture.DbContextFactory, new MemoryCache(new MemoryCacheOptions()),
        Mock.Of<IDateTimeOffsetAccessor>(d => d.Now == Start));

    private static MoMSeason Summer2026() => new(Guid.NewGuid(), 2026, 3, "Summer 2026", Start, End, Start);

    private static MoMBoardSeed Seed(MixEnum mix, ChartType type, IReadOnlyDictionary<Guid, double> deltas)
    {
        var id = Guid.NewGuid();
        var configuration = new TournamentConfiguration(id, Name.From($"{mix} {type}"),
            MoMScoring.ForBoard(mix, type), true, true) { MaxTime = MoMScoring.Window, AllowRepeats = false };
        return new MoMBoardSeed(id, mix, type, configuration, deltas);
    }

    [Fact]
    public async Task AddBoardsSeatsTheMissingBoardsWithTheirSnapshotBesideTheExistingOnes()
    {
        var repo = Repo();
        var season = Summer2026();
        var shared = Guid.NewGuid();
        var phoenix2Only = Guid.NewGuid();
        await repo.CreateSeason(season, new[]
        {
            Seed(MixEnum.Phoenix, ChartType.Double, new Dictionary<Guid, double> { [shared] = 24.9 }),
            Seed(MixEnum.Phoenix, ChartType.Single, new Dictionary<Guid, double>())
        }, CancellationToken.None);

        await repo.AddBoards(season.Id, new[]
        {
            Seed(MixEnum.Phoenix2, ChartType.Double, new Dictionary<Guid, double> { [shared] = 25.2 }),
            Seed(MixEnum.Phoenix2, ChartType.Single, new Dictionary<Guid, double> { [phoenix2Only] = 21.0 })
        }, CancellationToken.None);

        var keys = await repo.GetBoardKeys(season.Id, CancellationToken.None);
        Assert.Equal(4, keys.Count);
        Assert.Contains(new MoMBoardKey(MixEnum.Phoenix, ChartType.Double), keys);
        Assert.Contains(new MoMBoardKey(MixEnum.Phoenix, ChartType.Single), keys);
        Assert.Contains(new MoMBoardKey(MixEnum.Phoenix2, ChartType.Double), keys);
        Assert.Contains(new MoMBoardKey(MixEnum.Phoenix2, ChartType.Single), keys);

        // The snapshot is keyed per (season, mix, chart): the Phoenix row stays, the Phoenix 2
        // rows sit beside it.
        await using var ctx = await _fixture.DbContextFactory.CreateDbContextAsync();
        var levels = await ctx.Set<MoMChartLevelEntity>().Where(l => l.SeasonId == season.Id).ToArrayAsync();
        Assert.Equal(3, levels.Length);
        Assert.Equal(24.9, levels.Single(l => l.ChartId == shared && l.MixId == MixIds.Phoenix).Level);
        Assert.Equal(25.2, levels.Single(l => l.ChartId == shared && l.MixId == MixIds.Phoenix2).Level);
        Assert.Equal(21.0, levels.Single(l => l.ChartId == phoenix2Only).Level);

        // The read side sees the Phoenix 2 boards with their frozen tuning.
        var boards = await new EFMoMReadRepository(_fixture.DbContextFactory).GetBoards(new[] { season.Id }, CancellationToken.None);
        Assert.Equal(4, boards.Count);
        var phoenix2 = boards.Single(b => b.Mix == MixEnum.Phoenix2 && b.ChartType == ChartType.Double);
        Assert.Equal(.70, phoenix2.Configuration.Scoring.LetterGradeModifiers[PhoenixLetterGrade.APlus]);
        Assert.Equal(1.10, phoenix2.Configuration.Scoring.LetterGradeModifiers[PhoenixLetterGrade.AAAPlus]);
        Assert.Equal(MixEnum.Phoenix2, phoenix2.Configuration.Scoring.Mix);
        var phoenix = boards.Single(b => b.Mix == MixEnum.Phoenix && b.ChartType == ChartType.Double);
        Assert.Equal(.50, phoenix.Configuration.Scoring.LetterGradeModifiers[PhoenixLetterGrade.APlus]);
    }

    [Fact]
    public async Task AddBoardsNeverDoublesASnapshotRowTheSeasonAlreadyHolds()
    {
        var repo = Repo();
        var season = Summer2026();
        var chart = Guid.NewGuid();
        await repo.CreateSeason(season, new[]
        {
            Seed(MixEnum.Phoenix2, ChartType.Double, new Dictionary<Guid, double> { [chart] = 25.2 })
        }, CancellationToken.None);

        // A later heal for the same mix carries the same delta: the row is held, not doubled.
        await repo.AddBoards(season.Id, new[]
        {
            Seed(MixEnum.Phoenix2, ChartType.Single, new Dictionary<Guid, double> { [chart] = 25.2 })
        }, CancellationToken.None);

        await using var ctx = await _fixture.DbContextFactory.CreateDbContextAsync();
        var rows = await ctx.Set<MoMChartLevelEntity>()
            .CountAsync(l => l.SeasonId == season.Id && l.MixId == MixIds.Phoenix2 && l.ChartId == chart);
        Assert.Equal(1, rows);
        Assert.Equal(2, (await repo.GetBoardKeys(season.Id, CancellationToken.None)).Count);
    }
}
