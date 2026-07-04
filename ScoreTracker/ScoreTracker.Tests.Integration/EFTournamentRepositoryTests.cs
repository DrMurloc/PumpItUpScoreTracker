using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using ScoreTracker.Data.Persistence;
using ScoreTracker.EventCompetition.Contracts.Queries;
using ScoreTracker.EventCompetition.Infrastructure;
using ScoreTracker.EventCompetition.Infrastructure.Entities;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Tests.Integration.Fixtures;
using ChartType = ScoreTracker.SharedKernel.Enums.ChartType;

namespace ScoreTracker.Tests.Integration;

[Collection(IntegrationTestCollection.Name)]
[ExcludeFromCodeCoverage]
public sealed class EFTournamentRepositoryTests : IAsyncLifetime
{
    private readonly SqlServerFixture _fixture;

    public EFTournamentRepositoryTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // Several methods cache (GetAllTournaments, GetTournament, GetScoringLevelSnapshot, roles) — a
    // fresh repo + MemoryCache on the read side forces the DB path. `IChartRepository` is only used
    // by GetSession; tests that don't touch sessions use a bare Mock.Of, the session round-trip
    // tests stub it explicitly (chart loading is incidental to the persistence under test).
    private EFTournamentRepository BuildRepository() =>
        new(new MemoryCache(new MemoryCacheOptions()), Mock.Of<IChartRepository>(), _fixture.DbContextFactory);

    [Fact]
    public async Task CreateOrSaveTournamentInsertsAndGetAllTournamentsReturnsIt()
    {
        var id = Guid.NewGuid();
        var record = new TournamentRecord(id, "Test Tournament", CurrentParticipants: 0,
            TournamentType.Stamina, Location: "Remote", IsHighlighted: false, LinkOverride: null,
            StartDate: new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero),
            EndDate: new DateTimeOffset(2026, 5, 31, 0, 0, 0, TimeSpan.Zero),
            IsMoM: false);

        await BuildRepository().CreateOrSaveTournament(record, CancellationToken.None);

        var tournaments = (await BuildRepository().GetAllTournaments(CancellationToken.None)).ToList();
        Assert.Single(tournaments);
        Assert.Equal(id, tournaments[0].Id);
        Assert.Equal("Test Tournament", (string)tournaments[0].Name);
        Assert.Equal(TournamentType.Stamina, tournaments[0].Type);
        Assert.Equal("Remote", tournaments[0].Location);
    }

    [Fact]
    public async Task CreateOrSaveTournamentUpdatesExistingRowForSameId()
    {
        var id = Guid.NewGuid();
        var writer = BuildRepository();
        await writer.CreateOrSaveTournament(new TournamentRecord(id, "Old", 0, TournamentType.Stamina,
            "Remote", false, null, null, null, false), CancellationToken.None);
        await writer.CreateOrSaveTournament(new TournamentRecord(id, "New", 0, TournamentType.Stamina,
            "Onsite", true, null, null, null, true), CancellationToken.None);

        var tournaments = (await BuildRepository().GetAllTournaments(CancellationToken.None)).ToList();
        Assert.Single(tournaments);
        Assert.Equal("New", (string)tournaments[0].Name);
        Assert.Equal("Onsite", tournaments[0].Location);
        Assert.True(tournaments[0].IsHighlighted);
        Assert.True(tournaments[0].IsMoM);
    }

    [Fact]
    public async Task SetRoleThenRetrievingRolesReturnsTheAssignedRole()
    {
        var tournamentId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await BuildRepository().SetRole(tournamentId, userId, TournamentRole.TournamentOrganizer,
            CancellationToken.None);

        var roles = (await BuildRepository()
            .Handle(new GetTournamentRolesQuery(tournamentId), CancellationToken.None)).ToList();

        Assert.Single(roles);
        Assert.Equal(userId, roles[0].UserId);
        Assert.Equal(TournamentRole.TournamentOrganizer, roles[0].Role);
    }

    [Fact]
    public async Task SetRoleUpdatesExistingRoleForSameTournamentAndUser()
    {
        var tournamentId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var writer = BuildRepository();
        await writer.SetRole(tournamentId, userId, TournamentRole.Assistant, CancellationToken.None);
        await writer.SetRole(tournamentId, userId, TournamentRole.HeadTournamentOrganizer,
            CancellationToken.None);

        var roles = (await BuildRepository()
            .Handle(new GetTournamentRolesQuery(tournamentId), CancellationToken.None)).ToList();

        Assert.Single(roles);
        Assert.Equal(TournamentRole.HeadTournamentOrganizer, roles[0].Role);
    }

    [Fact]
    public async Task RevokeRoleRemovesTheRoleEntry()
    {
        var tournamentId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var writer = BuildRepository();
        await writer.SetRole(tournamentId, userId, TournamentRole.Assistant, CancellationToken.None);

        await writer.RevokeRole(tournamentId, userId, CancellationToken.None);

        var roles = (await BuildRepository()
            .Handle(new GetTournamentRolesQuery(tournamentId), CancellationToken.None)).ToList();

        Assert.Empty(roles);
    }

    [Fact]
    public async Task ScoringLevelSnapshotsRoundTripPreservesPerChartLevels()
    {
        var tournamentId = Guid.NewGuid();
        var chartA = Guid.NewGuid();
        var chartB = Guid.NewGuid();
        var snapshots = new[] { (chartA, 14.5), (chartB, 18.2) };

        await BuildRepository().CreateScoringLevelSnapshots(tournamentId, snapshots, CancellationToken.None);

        var retrieved = await BuildRepository().GetScoringLevelSnapshot(tournamentId, CancellationToken.None);

        Assert.NotNull(retrieved);
        Assert.Equal(2, retrieved!.Count);
        Assert.Equal(14.5, retrieved[chartA]);
        Assert.Equal(18.2, retrieved[chartB]);
    }

    [Fact]
    public async Task GetScoringLevelSnapshotReturnsNullWhenNoSnapshotExists()
    {
        var retrieved = await BuildRepository()
            .GetScoringLevelSnapshot(Guid.NewGuid(), CancellationToken.None);

        Assert.Null(retrieved);
    }

    private static Chart BuildChart(Guid chartId, MixEnum mix)
    {
        var song = new Song($"song_{chartId:N}", SongType.Arcade,
            new Uri("https://example.invalid/song.png"), TimeSpan.FromMinutes(2), "Artist", null);
        return new Chart(chartId, mix, song, ChartType.Single, DifficultyLevel.From(20), mix,
            null, null, new HashSet<Skill>());
    }

    private EFTournamentRepository BuildRepository(IChartRepository charts) =>
        new(new MemoryCache(new MemoryCacheOptions()), charts, _fixture.DbContextFactory);

    [Fact]
    public async Task SessionSavedWithPhoenix2RoundTripsItsMixAndLoadsChartsFromThatMix()
    {
        var tournamentId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var chartId = Guid.NewGuid();
        var chart = BuildChart(chartId, MixEnum.Phoenix2);
        var chartRepo = new Mock<IChartRepository>();
        chartRepo.Setup(c => c.GetCharts(MixEnum.Phoenix2, null, null, It.IsAny<IEnumerable<Guid>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { chart });

        var configuration = new TournamentConfiguration(tournamentId, "P2 Stamina",
            new ScoringConfiguration(), false, false);
        await BuildRepository(chartRepo.Object).CreateOrSaveTournament(configuration, CancellationToken.None);

        var session = new TournamentSession(userId, configuration, MixEnum.Phoenix2);
        session.Add(chart, 950000, PhoenixPlate.SuperbGame, isBroken: false);
        await BuildRepository(chartRepo.Object).SaveSession(session, CancellationToken.None);

        // The row itself carries the Phoenix 2 mix id, not just the in-memory round trip.
        await using (var context = await _fixture.DbContextFactory.CreateDbContextAsync())
        {
            var entity = await context.Set<UserTournamentSessionEntity>()
                .SingleAsync(e => e.TournamentId == tournamentId && e.UserId == userId);
            Assert.Equal(MixIds.Phoenix2, entity.MixId);
        }

        var loaded = await BuildRepository(chartRepo.Object)
            .GetSession(tournamentId, userId, CancellationToken.None);

        Assert.Equal(MixEnum.Phoenix2, loaded.Mix);
        var entry = Assert.Single(loaded.Entries);
        Assert.Equal(chartId, entry.Chart.Id);
        chartRepo.Verify(c => c.GetCharts(MixEnum.Phoenix2, null, null,
            It.Is<IEnumerable<Guid>?>(ids => ids != null && ids.Contains(chartId)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SessionSavedWithoutExplicitMixPersistsPhoenix()
    {
        var tournamentId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var chartId = Guid.NewGuid();
        var chart = BuildChart(chartId, MixEnum.Phoenix);
        var chartRepo = new Mock<IChartRepository>();
        chartRepo.Setup(c => c.GetCharts(MixEnum.Phoenix, null, null, It.IsAny<IEnumerable<Guid>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { chart });

        var configuration = new TournamentConfiguration(tournamentId, "P1 Stamina",
            new ScoringConfiguration(), false, false);
        await BuildRepository(chartRepo.Object).CreateOrSaveTournament(configuration, CancellationToken.None);

        var session = new TournamentSession(userId, configuration);
        session.Add(chart, 950000, PhoenixPlate.SuperbGame, isBroken: false);
        await BuildRepository(chartRepo.Object).SaveSession(session, CancellationToken.None);

        await using (var context = await _fixture.DbContextFactory.CreateDbContextAsync())
        {
            var entity = await context.Set<UserTournamentSessionEntity>()
                .SingleAsync(e => e.TournamentId == tournamentId && e.UserId == userId);
            Assert.Equal(MixIds.Phoenix, entity.MixId);
        }

        var loaded = await BuildRepository(chartRepo.Object)
            .GetSession(tournamentId, userId, CancellationToken.None);

        Assert.Equal(MixEnum.Phoenix, loaded.Mix);
    }
}
