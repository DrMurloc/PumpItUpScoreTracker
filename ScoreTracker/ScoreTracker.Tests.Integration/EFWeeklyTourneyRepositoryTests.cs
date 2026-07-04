using ScoreTracker.WeeklyChallenge.Infrastructure.Entities;
using ScoreTracker.WeeklyChallenge.Infrastructure;
using Microsoft.Extensions.Caching.Memory;
using ScoreTracker.Data.Repositories;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Records;
using ScoreTracker.SharedKernel.ValueTypes;
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
        await writer.RegisterWeeklyChart(MixEnum.Phoenix, new WeeklyTournamentChart(chartA, Expiration), CancellationToken.None);
        await writer.RegisterWeeklyChart(MixEnum.Phoenix, new WeeklyTournamentChart(chartB, Expiration), CancellationToken.None);

        var charts = (await BuildRepository().GetWeeklyCharts(MixEnum.Phoenix, CancellationToken.None)).ToList();

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

        await BuildRepository().SaveEntry(MixEnum.Phoenix, entry, CancellationToken.None);

        var entries = (await BuildRepository().GetEntries(MixEnum.Phoenix, chartId: null, CancellationToken.None)).ToList();

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
        await writer.SaveEntry(MixEnum.Phoenix, new WeeklyTournamentEntry(userId, chartId, 900000,
            PhoenixPlate.MarvelousGame, false, null, 17.0), CancellationToken.None);
        await writer.SaveEntry(MixEnum.Phoenix, new WeeklyTournamentEntry(userId, chartId, 970000,
            PhoenixPlate.SuperbGame, false, null, 18.0), CancellationToken.None);

        var entries = (await BuildRepository().GetEntries(MixEnum.Phoenix, chartId, CancellationToken.None)).ToList();

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
        await writer.SaveEntry(MixEnum.Phoenix, new WeeklyTournamentEntry(userId, chartA, 900000,
            PhoenixPlate.MarvelousGame, false, null, 17.0), CancellationToken.None);
        await writer.SaveEntry(MixEnum.Phoenix, new WeeklyTournamentEntry(userId, chartB, 950000,
            PhoenixPlate.SuperbGame, false, null, 18.0), CancellationToken.None);

        var onChartA = (await BuildRepository().GetEntries(MixEnum.Phoenix, chartA, CancellationToken.None)).ToList();

        Assert.Single(onChartA);
        Assert.Equal(chartA, onChartA[0].ChartId);
    }

    [Fact]
    public async Task ClearTheBoardRemovesBothWeeklyChartsAndUserEntries()
    {
        var userId = Guid.NewGuid();
        var chartId = Guid.NewGuid();
        var writer = BuildRepository();
        await writer.RegisterWeeklyChart(MixEnum.Phoenix, new WeeklyTournamentChart(chartId, Expiration), CancellationToken.None);
        await writer.SaveEntry(MixEnum.Phoenix, new WeeklyTournamentEntry(userId, chartId, 900000,
            PhoenixPlate.MarvelousGame, false, null, 17.0), CancellationToken.None);

        await writer.ClearTheBoard(MixEnum.Phoenix, CancellationToken.None);

        var charts = (await BuildRepository().GetWeeklyCharts(MixEnum.Phoenix, CancellationToken.None)).ToList();
        var entries = (await BuildRepository().GetEntries(MixEnum.Phoenix, null, CancellationToken.None)).ToList();

        Assert.Empty(charts);
        Assert.Empty(entries);
    }

    [Fact]
    public async Task WriteAlreadyPlayedChartsAddsOnlyChartsNotAlreadyTracked()
    {
        var existing = Guid.NewGuid();
        var newChart = Guid.NewGuid();
        var writer = BuildRepository();
        await writer.WriteAlreadyPlayedCharts(MixEnum.Phoenix, new[] { existing }, CancellationToken.None);
        await writer.WriteAlreadyPlayedCharts(MixEnum.Phoenix, new[] { existing, newChart }, CancellationToken.None);

        var allCharts = (await BuildRepository().GetAlreadyPlayedCharts(MixEnum.Phoenix, CancellationToken.None)).ToList();

        Assert.Equal(2, allCharts.Count);
        Assert.Contains(existing, allCharts);
        Assert.Contains(newChart, allCharts);
    }

    [Fact]
    public async Task BoardsAreIsolatedPerMix()
    {
        // Parallel boards per mix (locked decision): clearing or reading one mix's board
        // never touches the other's charts or entries.
        var userId = Guid.NewGuid();
        var phoenixChart = Guid.NewGuid();
        var phoenix2Chart = Guid.NewGuid();
        var writer = BuildRepository();
        await writer.RegisterWeeklyChart(MixEnum.Phoenix, new WeeklyTournamentChart(phoenixChart, Expiration),
            CancellationToken.None);
        await writer.RegisterWeeklyChart(MixEnum.Phoenix2, new WeeklyTournamentChart(phoenix2Chart, Expiration),
            CancellationToken.None);
        await writer.SaveEntry(MixEnum.Phoenix, new WeeklyTournamentEntry(userId, phoenixChart, 900000,
            PhoenixPlate.MarvelousGame, false, null, 17.0), CancellationToken.None);
        await writer.SaveEntry(MixEnum.Phoenix2, new WeeklyTournamentEntry(userId, phoenix2Chart, 950000,
            PhoenixPlate.SuperbGame, false, null, 18.0), CancellationToken.None);

        await writer.ClearTheBoard(MixEnum.Phoenix, CancellationToken.None);

        var reader = BuildRepository();
        Assert.Empty(await reader.GetWeeklyCharts(MixEnum.Phoenix, CancellationToken.None));
        Assert.Empty(await reader.GetEntries(MixEnum.Phoenix, null, CancellationToken.None));
        var phoenix2Charts = (await reader.GetWeeklyCharts(MixEnum.Phoenix2, CancellationToken.None)).ToList();
        var phoenix2Entries = (await reader.GetEntries(MixEnum.Phoenix2, null, CancellationToken.None)).ToList();
        Assert.Single(phoenix2Charts);
        Assert.Equal(phoenix2Chart, phoenix2Charts[0].ChartId);
        Assert.Single(phoenix2Entries);
        Assert.Equal(950000, (int)phoenix2Entries[0].Score);
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

        await BuildRepository().WriteHistories(MixEnum.Phoenix, new[] { history }, CancellationToken.None);

        var pastEntries = (await BuildRepository().GetPastEntries(MixEnum.Phoenix, date, CancellationToken.None)).ToList();

        Assert.Single(pastEntries);
        Assert.Equal(userId, pastEntries[0].UserId);
        Assert.Equal(chartId, pastEntries[0].ChartId);
        Assert.Equal(970000, (int)pastEntries[0].Score);
        Assert.Equal(PhoenixPlate.SuperbGame, pastEntries[0].Plate);
        Assert.Equal(18.5, pastEntries[0].CompetitiveLevel);
    }
}
