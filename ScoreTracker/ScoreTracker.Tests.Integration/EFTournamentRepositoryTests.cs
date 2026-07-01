using Microsoft.Extensions.Caching.Memory;
using Moq;
using ScoreTracker.EventCompetition.Contracts.Queries;
using ScoreTracker.EventCompetition.Infrastructure;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Tests.Integration.Fixtures;

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
    // by GetSession (rich-aggregate session loading, not covered here), so a Mock.Of is safe.
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
}
