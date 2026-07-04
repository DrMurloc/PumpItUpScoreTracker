using Moq;
using ScoreTracker.Data.Persistence;
using ScoreTracker.EventCompetition.Infrastructure;
using ScoreTracker.EventCompetition.Infrastructure.Entities;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Tests.Integration.Fixtures;
using ChartType = ScoreTracker.SharedKernel.Enums.ChartType;

namespace ScoreTracker.Tests.Integration;

[Collection(IntegrationTestCollection.Name)]
[ExcludeFromCodeCoverage]
public sealed class EFQualifiersRepositoryTests : IAsyncLifetime
{
    private readonly SqlServerFixture _fixture;

    public EFQualifiersRepositoryTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private static Chart BuildChart(Guid chartId, MixEnum mix)
    {
        var song = new Song($"song_{chartId:N}", SongType.Arcade,
            new Uri("https://example.invalid/song.png"), TimeSpan.FromMinutes(2), "Artist", null);
        return new Chart(chartId, mix, song, ChartType.Single, DifficultyLevel.From(20), mix,
            null, null, new HashSet<Skill>());
    }

    private async Task InsertConfigurationRow(Guid tournamentId, Guid mixId, Guid chartId)
    {
        await using var context = await _fixture.DbContextFactory.CreateDbContextAsync();
        context.Set<QualifiersConfigurationEntity>().Add(new QualifiersConfigurationEntity
        {
            TournamentId = tournamentId,
            MixId = mixId,
            ScoringType = "Phoenix",
            Charts = chartId.ToString(),
            AllCharts = false,
            NotificationChannel = 1,
            ChartPlayCount = 2,
            CutoffTime = null
        });
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task GetQualifiersConfigurationReadsThePinnedPhoenix2MixAndFetchesItsCharts()
    {
        var tournamentId = Guid.NewGuid();
        var chartId = Guid.NewGuid();
        await InsertConfigurationRow(tournamentId, MixIds.Phoenix2, chartId);

        var chartRepo = new Mock<IChartRepository>();
        chartRepo.Setup(c => c.GetCharts(It.IsAny<MixEnum>(), null, null, It.IsAny<IEnumerable<Guid>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { BuildChart(chartId, MixEnum.Phoenix2) });
        var repository = new EFQualifiersRepository(chartRepo.Object, _fixture.DbContextFactory);

        var configuration = await repository.GetQualifiersConfiguration(tournamentId, CancellationToken.None);

        Assert.Equal(MixEnum.Phoenix2, configuration.Mix);
        Assert.Single(configuration.Charts, c => c.Id == chartId);
        chartRepo.Verify(c => c.GetCharts(MixEnum.Phoenix2, null, null,
            It.Is<IEnumerable<Guid>?>(ids => ids != null && ids.Contains(chartId)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetQualifiersConfigurationDefaultsBackfilledRowsToPhoenix()
    {
        var tournamentId = Guid.NewGuid();
        var chartId = Guid.NewGuid();
        await InsertConfigurationRow(tournamentId, MixIds.Phoenix, chartId);

        var chartRepo = new Mock<IChartRepository>();
        chartRepo.Setup(c => c.GetCharts(It.IsAny<MixEnum>(), null, null, It.IsAny<IEnumerable<Guid>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { BuildChart(chartId, MixEnum.Phoenix) });
        var repository = new EFQualifiersRepository(chartRepo.Object, _fixture.DbContextFactory);

        var configuration = await repository.GetQualifiersConfiguration(tournamentId, CancellationToken.None);

        Assert.Equal(MixEnum.Phoenix, configuration.Mix);
        chartRepo.Verify(c => c.GetCharts(MixEnum.Phoenix, null, null,
            It.IsAny<IEnumerable<Guid>?>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
