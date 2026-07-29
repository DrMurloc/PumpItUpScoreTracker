using Moq;
using ScoreTracker.Data.Persistence;
using ScoreTracker.EventCompetition.Infrastructure;
using ScoreTracker.EventCompetition.Infrastructure.Entities;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Domain.Models;
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

    private static readonly DateTimeOffset SubmittedAt = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    private async Task<(EFQualifiersRepository Repo, QualifiersConfiguration Config, Guid ChartId)>
        BuildRepositoryWithOneChart(Guid tournamentId)
    {
        var chartId = Guid.NewGuid();
        await InsertConfigurationRow(tournamentId, MixIds.Phoenix, chartId);

        var chartRepo = new Mock<IChartRepository>();
        chartRepo.Setup(c => c.GetCharts(It.IsAny<MixEnum>(), null, null, It.IsAny<IEnumerable<Guid>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { BuildChart(chartId, MixEnum.Phoenix) });
        var repository = new EFQualifiersRepository(chartRepo.Object, _fixture.DbContextFactory);
        var config = await repository.GetQualifiersConfiguration(tournamentId, CancellationToken.None);
        return (repository, config, chartId);
    }

    [Fact]
    public async Task SubmissionSourceAndTimestampSurviveARoundTrip()
    {
        var tournamentId = Guid.NewGuid();
        var (repository, config, chartId) = await BuildRepositoryWithOneChart(tournamentId);

        var entry = new UserQualifiers(config, Name.From("roundtrip"), Guid.NewGuid(),
            new Dictionary<Guid, UserQualifiers.Submission>());
        entry.AddManualScore(chartId, 987000, new Uri("https://example.invalid/shot.png"), SubmittedAt);
        await repository.SaveQualifiers(tournamentId, entry, CancellationToken.None);

        var reloaded = await repository.GetQualifiers(tournamentId, Name.From("roundtrip"), config,
            CancellationToken.None);

        Assert.NotNull(reloaded);
        var submission = reloaded!.Submissions[chartId];
        Assert.Equal(SubmissionSource.Manual, submission.Source);
        Assert.Equal(SubmittedAt, submission.SubmittedAt);
        Assert.Equal("https://example.invalid/shot.png", submission.PhotoUrl?.ToString());
    }

    [Fact]
    public async Task RowsWrittenBeforeTheSourceFieldExistedInferItFromThePhoto()
    {
        var tournamentId = Guid.NewGuid();
        var (repository, config, chartId) = await BuildRepositoryWithOneChart(tournamentId);

        // The exact shape the old serializer produced: no Source, no SubmittedAt.
        var legacyJson =
            $"[{{\"ChartId\":\"{chartId}\",\"Score\":950000,\"PhotoUrl\":\"https://example.invalid/old.png\"}}," +
            $"{{\"ChartId\":\"{Guid.NewGuid()}\",\"Score\":940000,\"PhotoUrl\":null}}]";
        await using (var context = await _fixture.DbContextFactory.CreateDbContextAsync())
        {
            context.Set<UserQualifierEntity>().Add(new UserQualifierEntity
            {
                Id = Guid.NewGuid(),
                TournamentId = tournamentId,
                Name = "legacy",
                Entries = legacyJson
            });
            await context.SaveChangesAsync();
        }

        var reloaded = await repository.GetQualifiers(tournamentId, Name.From("legacy"), config,
            CancellationToken.None);

        Assert.NotNull(reloaded);
        // A photo means somebody typed it in; no photo means the importer wrote it.
        Assert.Equal(SubmissionSource.Manual, reloaded!.Submissions[chartId].Source);
        Assert.Contains(reloaded.Submissions.Values, s => s.Source == SubmissionSource.OfficialImport);
    }

    [Fact]
    public async Task DeleteQualifiersRemovesTheEntryButKeepsItsHistory()
    {
        var tournamentId = Guid.NewGuid();
        var (repository, config, chartId) = await BuildRepositoryWithOneChart(tournamentId);

        var entry = new UserQualifiers(config, Name.From("dupe"), null,
            new Dictionary<Guid, UserQualifiers.Submission>());
        entry.AddManualScore(chartId, 960000, new Uri("https://example.invalid/a.png"), SubmittedAt);
        await repository.SaveQualifiers(tournamentId, entry, CancellationToken.None);

        await repository.DeleteQualifiers(tournamentId, Name.From("dupe"), CancellationToken.None);

        Assert.Null(await repository.GetQualifiers(tournamentId, Name.From("dupe"), config, CancellationToken.None));
        await using var context = await _fixture.DbContextFactory.CreateDbContextAsync();
        Assert.Contains(context.Set<UserQualifierHistoryEntity>(),
            h => h.TournamentId == tournamentId && h.Name == "dupe");
    }

    [Fact]
    public async Task RemovingOneSubmissionLeavesTheRestOfTheEntryIntact()
    {
        var tournamentId = Guid.NewGuid();
        var (repository, config, chartId) = await BuildRepositoryWithOneChart(tournamentId);
        var otherChartId = Guid.NewGuid();

        var entry = new UserQualifiers(config, Name.From("trimmed"), null,
            new Dictionary<Guid, UserQualifiers.Submission>());
        entry.AddManualScore(chartId, 960000, new Uri("https://example.invalid/a.png"), SubmittedAt);
        entry.AddImportedScore(otherChartId, 970000, SubmittedAt);
        await repository.SaveQualifiers(tournamentId, entry, CancellationToken.None);

        entry.Submissions.Remove(chartId);
        await repository.SaveQualifiers(tournamentId, entry, CancellationToken.None);

        var reloaded = await repository.GetQualifiers(tournamentId, Name.From("trimmed"), config,
            CancellationToken.None);
        Assert.NotNull(reloaded);
        Assert.Single(reloaded!.Submissions);
        Assert.True(reloaded.Submissions.ContainsKey(otherChartId));
    }

    [Fact]
    public async Task FirstSubmissionDatesReportTheEarliestSnapshotPerEntrant()
    {
        var tournamentId = Guid.NewGuid();
        var (repository, config, chartId) = await BuildRepositoryWithOneChart(tournamentId);

        var entry = new UserQualifiers(config, Name.From("early"), null,
            new Dictionary<Guid, UserQualifiers.Submission>());
        entry.AddManualScore(chartId, 950000, new Uri("https://example.invalid/1.png"), SubmittedAt);
        await repository.SaveQualifiers(tournamentId, entry, CancellationToken.None);
        entry.AddManualScore(chartId, 960000, new Uri("https://example.invalid/2.png"), SubmittedAt.AddHours(2));
        await repository.SaveQualifiers(tournamentId, entry, CancellationToken.None);

        var dates = await repository.GetFirstSubmissionDates(tournamentId, CancellationToken.None);

        Assert.True(dates.ContainsKey("early"));
        // Two saves, two snapshots, and the earlier one is what dates the entry.
        await using var context = await _fixture.DbContextFactory.CreateDbContextAsync();
        var snapshots = context.Set<UserQualifierHistoryEntity>()
            .Where(h => h.TournamentId == tournamentId && h.Name == "early")
            .Select(h => h.RecordedDate).ToArray();
        Assert.Equal(2, snapshots.Length);
        Assert.Equal(snapshots.Min(), dates["early"]);
    }

    [Fact]
    public async Task UnregisterUserFromTournamentTurnsAutoSubmitBackOff()
    {
        var tournamentId = Guid.NewGuid();
        var (repository, _, _) = await BuildRepositoryWithOneChart(tournamentId);
        var userId = Guid.NewGuid();

        await repository.RegisterUserToTournament(tournamentId, userId, CancellationToken.None);
        Assert.Contains(userId, await repository.GetRegisteredUsers(tournamentId, CancellationToken.None));

        await repository.UnregisterUserFromTournament(tournamentId, userId, CancellationToken.None);

        Assert.DoesNotContain(userId, await repository.GetRegisteredUsers(tournamentId, CancellationToken.None));
    }
}
