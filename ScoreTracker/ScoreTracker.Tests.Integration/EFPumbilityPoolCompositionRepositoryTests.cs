using ScoreTracker.ChartIntelligence.Contracts;
using ScoreTracker.ChartIntelligence.Infrastructure;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Tests.Integration.Fixtures;

namespace ScoreTracker.Tests.Integration;

[Collection(IntegrationTestCollection.Name)]
[ExcludeFromCodeCoverage]
public sealed class EFPumbilityPoolCompositionRepositoryTests : IAsyncLifetime
{
    private static readonly DateTimeOffset ComputedAt = new(2026, 8, 16, 10, 30, 0, TimeSpan.Zero);
    private readonly SqlServerFixture _fixture;

    public EFPumbilityPoolCompositionRepositoryTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private EFPumbilityPoolCompositionRepository BuildRepository() => new(_fixture.DbContextFactory);

    [Fact]
    public async Task ANeverWrittenMixReadsAsNull()
    {
        Assert.Null(await BuildRepository().Get(MixEnum.Phoenix2, CancellationToken.None));
    }

    [Fact]
    public async Task SaveAndGetRoundTripEveryBandInFloorOrder()
    {
        var composition = new PumbilityPoolCompositionRecord(MixEnum.Phoenix2, ComputedAt, 67, new[]
        {
            Band("[P.B] GOLD", "[P.B] GOLD", 15_000, 16_000, 8, 400, 6_360, 12_442.5, 5_524.25, 75.5,
                new Dictionary<PhoenixLetterGrade, int> { [PhoenixLetterGrade.SSSPlus] = 300, [PhoenixLetterGrade.AAA] = 100 }),
            Band("[P.B] PLATINUM", "[P.B] PLATINUM", 16_000, 17_000, 19, 950, 17_670, 30_000, 13_000, 180,
                new Dictionary<PhoenixLetterGrade, int> { [PhoenixLetterGrade.S] = 950 }),
            Band("ABYSS ABSOLUTE", "ABYSS ABSOLUTE", 20_000, null, 0, 0, 0, 0, 0, 0,
                new Dictionary<PhoenixLetterGrade, int>())
        });

        await BuildRepository().Save(composition, CancellationToken.None);
        var read = await BuildRepository().Get(MixEnum.Phoenix2, CancellationToken.None);

        Assert.NotNull(read);
        Assert.Equal(MixEnum.Phoenix2, read!.Mix);
        Assert.Equal(ComputedAt, read.ComputedAt);
        Assert.Equal(67, read.PoolsCounted);
        Assert.Equal(new[] { "[P.B] GOLD", "[P.B] PLATINUM", "ABYSS ABSOLUTE" }, read.Bands.Select(b => b.Key));
        var gold = read.Bands[0];
        Assert.Equal("[P.B] GOLD", gold.Title);
        Assert.Equal(15_000, gold.Floor);
        Assert.Equal(16_000, gold.Ceiling);
        Assert.Equal(8, gold.Players);
        Assert.Equal(400, gold.ChartsPooled);
        Assert.Equal(6_360, gold.LevelSum);
        Assert.Equal(12_442.5, gold.LevelPart);
        Assert.Equal(5_524.25, gold.ScorePart);
        Assert.Equal(75.5, gold.PlatePart);
        Assert.Equal(300, gold.GradeCounts[PhoenixLetterGrade.SSSPlus]);
        Assert.Equal(100, gold.GradeCounts[PhoenixLetterGrade.AAA]);
        Assert.Null(read.Bands[2].Ceiling);
        Assert.Empty(read.Bands[2].GradeCounts);
    }

    [Fact]
    public async Task SaveReplacesTheMixWholesaleAndLeavesOtherMixesAlone()
    {
        var repository = BuildRepository();
        await repository.Save(new PumbilityPoolCompositionRecord(MixEnum.Phoenix2, ComputedAt, 1, new[]
        {
            Band("[P.B] GOLD", "[P.B] GOLD", 15_000, 16_000, 1, 50, 800, 100, 10, 1, new Dictionary<PhoenixLetterGrade, int>()),
            Band("stale", null, 0, 1, 1, 50, 800, 100, 10, 1, new Dictionary<PhoenixLetterGrade, int>())
        }), CancellationToken.None);
        await repository.Save(new PumbilityPoolCompositionRecord(MixEnum.Phoenix, ComputedAt, 1, new[]
        {
            Band("20k", null, 20_000, 30_000, 1, 50, 900, 200, 20, 0, new Dictionary<PhoenixLetterGrade, int>())
        }), CancellationToken.None);

        await repository.Save(new PumbilityPoolCompositionRecord(MixEnum.Phoenix2, ComputedAt.AddDays(1), 2, new[]
        {
            Band("[P.B] GOLD", "[P.B] GOLD", 15_000, 16_000, 2, 100, 1_600, 200, 20, 2, new Dictionary<PhoenixLetterGrade, int>())
        }), CancellationToken.None);

        var phoenix2 = await repository.Get(MixEnum.Phoenix2, CancellationToken.None);
        Assert.Equal(new[] { "[P.B] GOLD" }, phoenix2!.Bands.Select(b => b.Key));
        Assert.Equal(2, phoenix2.PoolsCounted);
        Assert.Equal(ComputedAt.AddDays(1), phoenix2.ComputedAt);
        var phoenix = await repository.Get(MixEnum.Phoenix, CancellationToken.None);
        Assert.Equal(new[] { "20k" }, phoenix!.Bands.Select(b => b.Key));
    }

    private static PumbilityPoolBandRecord Band(string key, string? title, double floor, double? ceiling, int players,
        int charts, double levelSum, double levelPart, double scorePart, double platePart,
        IReadOnlyDictionary<PhoenixLetterGrade, int> grades)
    {
        return new PumbilityPoolBandRecord(key, title, floor, ceiling, players, charts, levelSum, levelPart, scorePart,
            platePart, grades);
    }
}
