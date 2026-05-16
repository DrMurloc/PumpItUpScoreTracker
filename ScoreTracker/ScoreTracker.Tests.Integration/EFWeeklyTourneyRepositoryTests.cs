using Microsoft.Extensions.Caching.Memory;
using ScoreTracker.Data.Repositories;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.ValueTypes;
using ScoreTracker.Tests.Integration.Fixtures;

namespace ScoreTracker.Tests.Integration;

[Collection(IntegrationTestCollection.Name)]
[ExcludeFromCodeCoverage]
public sealed class EFWeeklyTourneyRepositoryTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Expiration = new(2026, 5, 23, 0, 0, 0, TimeSpan.Zero);

    private readonly SqlServerFixture _fixture;

    public EFWeeklyTourneyRepositoryTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // GetWeeklyCharts caches; fresh cache on read forces DB.
    private EFWeeklyTourneyRepository BuildRepository() =>
        new(_fixture.DbContextFactory, new MemoryCache(new MemoryCacheOptions()));

    [Fact]
    public async Task RegisterWeeklyChartAndGetWeeklyChartsRoundTrip()
    {
        var chartA = Guid.NewGuid();
        var chartB = Guid.NewGuid();
        var writer = BuildRepository();
        await writer.RegisterWeeklyChart(new WeeklyTournamentChart(chartA, Expiration), CancellationToken.None);
        await writer.RegisterWeeklyChart(new WeeklyTournamentChart(chartB, Expiration), CancellationToken.None);

        var charts = (await BuildRepository().GetWeeklyCharts(CancellationToken.None)).ToList();

        Assert.Equal(2, charts.Count);
        Assert.Contains(charts, c => c.ChartId == chartA);
        Assert.Contains(charts, c => c.ChartId == chartB);
    }

    [Fact]
    public async Task SaveEntryAndGetEntriesRoundTripPreservesAllFields()
    {
        var userId = Guid.NewGuid();
        var chartId = Guid.NewGuid();
        var entry = new WeeklyTournamentEntry(userId, chartId,
            Score: PhoenixScore.From(950000), Plate: PhoenixPlate.SuperbGame,
            IsBroken: false, PhotoUrl: new Uri("https://example.invalid/photo.png"),
            CompetitiveLevel: 18.5);

        await BuildRepository().SaveEntry(entry, CancellationToken.None);

        var entries = (await BuildRepository().GetEntries(chartId: null, CancellationToken.None)).ToList();

        Assert.Single(entries);
        Assert.Equal(userId, entries[0].UserId);
        Assert.Equal(chartId, entries[0].ChartId);
        Assert.Equal(950000, (int)entries[0].Score);
        Assert.Equal(PhoenixPlate.SuperbGame, entries[0].Plate);
        Assert.False(entries[0].IsBroken);
        Assert.Equal(new Uri("https://example.invalid/photo.png"), entries[0].PhotoUrl);
        Assert.Equal(18.5, entries[0].CompetitiveLevel);
    }

    [Fact]
    public async Task SaveEntryUpdatesExistingForSameUserAndChart()
    {
        var userId = Guid.NewGuid();
        var chartId = Guid.NewGuid();
        var writer = BuildRepository();
        await writer.SaveEntry(new WeeklyTournamentEntry(userId, chartId, 900000,
            PhoenixPlate.MarvelousGame, false, null, 17.0), CancellationToken.None);
        await writer.SaveEntry(new WeeklyTournamentEntry(userId, chartId, 970000,
            PhoenixPlate.SuperbGame, false, null, 18.0), CancellationToken.None);

        var entries = (await BuildRepository().GetEntries(chartId, CancellationToken.None)).ToList();

        Assert.Single(entries);
        Assert.Equal(970000, (int)entries[0].Score);
        Assert.Equal(PhoenixPlate.SuperbGame, entries[0].Plate);
        Assert.Equal(18.0, entries[0].CompetitiveLevel);
    }

    [Fact]
    public async Task GetEntriesFiltersByChartId()
    {
        var userId = Guid.NewGuid();
        var chartA = Guid.NewGuid();
        var chartB = Guid.NewGuid();
        var writer = BuildRepository();
        await writer.SaveEntry(new WeeklyTournamentEntry(userId, chartA, 900000,
            PhoenixPlate.MarvelousGame, false, null, 17.0), CancellationToken.None);
        await writer.SaveEntry(new WeeklyTournamentEntry(userId, chartB, 950000,
            PhoenixPlate.SuperbGame, false, null, 18.0), CancellationToken.None);

        var onChartA = (await BuildRepository().GetEntries(chartA, CancellationToken.None)).ToList();

        Assert.Single(onChartA);
        Assert.Equal(chartA, onChartA[0].ChartId);
    }

    [Fact]
    public async Task ClearTheBoardRemovesBothWeeklyChartsAndUserEntries()
    {
        var userId = Guid.NewGuid();
        var chartId = Guid.NewGuid();
        var writer = BuildRepository();
        await writer.RegisterWeeklyChart(new WeeklyTournamentChart(chartId, Expiration), CancellationToken.None);
        await writer.SaveEntry(new WeeklyTournamentEntry(userId, chartId, 900000,
            PhoenixPlate.MarvelousGame, false, null, 17.0), CancellationToken.None);

        await writer.ClearTheBoard(CancellationToken.None);

        var charts = (await BuildRepository().GetWeeklyCharts(CancellationToken.None)).ToList();
        var entries = (await BuildRepository().GetEntries(null, CancellationToken.None)).ToList();

        Assert.Empty(charts);
        Assert.Empty(entries);
    }

    [Fact]
    public async Task WriteAlreadyPlayedChartsAddsOnlyChartsNotAlreadyTracked()
    {
        var existing = Guid.NewGuid();
        var newChart = Guid.NewGuid();
        var writer = BuildRepository();
        await writer.WriteAlreadyPlayedCharts(new[] { existing }, CancellationToken.None);
        await writer.WriteAlreadyPlayedCharts(new[] { existing, newChart }, CancellationToken.None);

        var allCharts = (await BuildRepository().GetAlreadyPlayedCharts(CancellationToken.None)).ToList();

        Assert.Equal(2, allCharts.Count);
        Assert.Contains(existing, allCharts);
        Assert.Contains(newChart, allCharts);
    }

    [Fact]
    public async Task WriteHistoriesAndGetPastEntriesRoundTrip()
    {
        // Weekly cycle rollover archives current placings into UserWeeklyPlacing. GetPastEntries(date)
        // is how the saga reads them back for the historical leaderboard view. Pin the round-trip.
        var date = new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero);
        var userId = Guid.NewGuid();
        var chartId = Guid.NewGuid();
        var history = new UserTourneyHistory(userId, chartId, ReceivedOn: date, Place: 1,
            CompetitiveLevel: 18.5, Score: PhoenixScore.From(970000),
            Plate: PhoenixPlate.SuperbGame, IsBroken: false);

        await BuildRepository().WriteHistories(new[] { history }, CancellationToken.None);

        var pastEntries = (await BuildRepository().GetPastEntries(date, CancellationToken.None)).ToList();

        Assert.Single(pastEntries);
        Assert.Equal(userId, pastEntries[0].UserId);
        Assert.Equal(chartId, pastEntries[0].ChartId);
        Assert.Equal(970000, (int)pastEntries[0].Score);
        Assert.Equal(PhoenixPlate.SuperbGame, pastEntries[0].Plate);
        Assert.Equal(18.5, pastEntries[0].CompetitiveLevel);
    }
}
