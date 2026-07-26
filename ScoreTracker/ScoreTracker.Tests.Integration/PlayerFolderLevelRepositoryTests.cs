using Microsoft.EntityFrameworkCore;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.PlayerProgress.Infrastructure;
using ScoreTracker.PlayerProgress.Infrastructure.Entities;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.Integration.Fixtures;

namespace ScoreTracker.Tests.Integration;

[Collection(IntegrationTestCollection.Name)]
[ExcludeFromCodeCoverage]
public sealed class PlayerFolderLevelRepositoryTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 7, 26, 12, 0, 0, TimeSpan.Zero);

    private readonly SqlServerFixture _fixture;

    public PlayerFolderLevelRepositoryTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private static FolderLevelRecord Folder(ChartType type, int level, int size, int played, int average,
        MixEnum mix = MixEnum.Phoenix) =>
        new(mix, type, DifficultyLevel.From(level), size, played, average);

    [Fact]
    public async Task SavingTheSameFolderTwiceUpsertsRatherThanDuplicating()
    {
        var userId = Guid.NewGuid();
        var repo = new EFPlayerFolderLevelRepository(_fixture.DbContextFactory);

        await repo.Save(userId, new[] { Folder(ChartType.Single, 22, 97, 60, 930_000) }, Now,
            CancellationToken.None);
        await repo.Save(userId, new[] { Folder(ChartType.Single, 22, 97, 90, 934_245) }, Now.AddDays(1),
            CancellationToken.None);

        var levels = (await repo.GetFolderLevels(MixEnum.Phoenix, userId, CancellationToken.None)).ToArray();

        var folder = Assert.Single(levels);
        Assert.Equal(90, folder.Played);
        Assert.Equal(934_245, folder.AverageScore);
        Assert.Equal(92, folder.CompletionPercent);
        Assert.Equal(80, folder.Tier);
    }

    [Fact]
    public async Task SinglesAndDoublesAtTheSameLevelAreSeparateRows()
    {
        var userId = Guid.NewGuid();
        var repo = new EFPlayerFolderLevelRepository(_fixture.DbContextFactory);

        await repo.Save(userId, new[]
        {
            Folder(ChartType.Single, 18, 189, 185, 986_198),
            Folder(ChartType.Double, 18, 184, 180, 991_014),
            Folder(ChartType.CoOp, 2, 116, 46, 978_913)
        }, Now, CancellationToken.None);

        var levels = (await repo.GetFolderLevels(MixEnum.Phoenix, userId, CancellationToken.None))
            .ToDictionary(l => l.Folder);

        Assert.Equal(3, levels.Count);
        Assert.Equal(986_198, levels["S18"].AverageScore);
        Assert.Equal(991_014, levels["D18"].AverageScore);
        Assert.Equal(46, levels["CoOp2"].Played);
    }

    [Fact]
    public async Task FolderLevelsAreScopedPerMix()
    {
        var userId = Guid.NewGuid();
        var repo = new EFPlayerFolderLevelRepository(_fixture.DbContextFactory);

        await repo.Save(userId, new[]
        {
            Folder(ChartType.Single, 22, 97, 90, 934_245),
            Folder(ChartType.Single, 22, 40, 4, 934_245, MixEnum.Phoenix2)
        }, Now, CancellationToken.None);

        var phoenix = (await repo.GetFolderLevels(MixEnum.Phoenix, userId, CancellationToken.None)).ToArray();
        var phoenix2 = (await repo.GetFolderLevels(MixEnum.Phoenix2, userId, CancellationToken.None)).ToArray();

        Assert.Equal(90, Assert.Single(phoenix).Played);
        Assert.Equal(4, Assert.Single(phoenix2).Played);

        // The same average sits either side of the Phoenix 2 AA+ floor, which moved to 940k.
        Assert.Equal(PhoenixLetterGrade.AAPlus, phoenix[0].Grade);
        Assert.Equal(PhoenixLetterGrade.AA, phoenix2[0].Grade);
    }

    [Fact]
    public async Task AnUnchangedFolderKeepsItsOriginalUpdatedStamp()
    {
        var userId = Guid.NewGuid();
        var repo = new EFPlayerFolderLevelRepository(_fixture.DbContextFactory);
        var row = Folder(ChartType.Single, 20, 135, 128, 973_756);

        await repo.Save(userId, new[] { row }, Now, CancellationToken.None);
        await repo.Save(userId, new[] { row }, Now.AddDays(7), CancellationToken.None);

        await using var database = await _fixture.DbContextFactory.CreateDbContextAsync();
        var stamps = database.Set<PlayerFolderLevelEntity>().Select(e => e.UpdatedAt).ToArray();

        Assert.Equal(Now, Assert.Single(stamps));
    }
}
