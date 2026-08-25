using System.Text.Json;
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
using ScoreTracker.Tests.Integration.TestData;
using ChartType = ScoreTracker.SharedKernel.Enums.ChartType;

namespace ScoreTracker.Tests.Integration;

[Collection(IntegrationTestCollection.Name)]
[ExcludeFromCodeCoverage]
public sealed class EFTournamentRepositoryTests : IAsyncLifetime
{
    // Inside every seeded season's window, so IsCurrent/highlight derivation is deterministic.
    private static readonly DateTimeOffset Now = new(2026, 8, 14, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset SeasonStart = new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset SeasonEnd = new(2026, 9, 30, 23, 59, 59, TimeSpan.FromHours(-5));

    private readonly SqlServerFixture _fixture;

    public EFTournamentRepositoryTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // Several methods cache (GetAllTournaments, GetTournament, roles) — a fresh repo +
    // MemoryCache on the read side forces the DB path.
    private EFTournamentRepository BuildRepository() => BuildRepository(Now);

    private EFTournamentRepository BuildRepository(DateTimeOffset now) =>
        new(new MemoryCache(new MemoryCacheOptions()), _fixture.DbContextFactory,
            Mock.Of<ICurrentUserAccessor>(), Mock.Of<IDateTimeOffsetAccessor>(d => d.Now == now));

    /// <summary>
    ///     A season + board pair seeded the way the migration and the cycle write them; the
    ///     frozen config serializes through the same DTO production uses, so the board path's
    ///     deserialization is the real one.
    /// </summary>
    private async Task<(Guid SeasonId, Guid BoardId)> SeedBoard(MixEnum mix, ChartType chartType,
        string seasonName = "Winter 2099", int? year = 2099, byte? quarter = 1,
        DateTimeOffset? startsAt = null, DateTimeOffset? endsAt = null)
    {
        var seasonId = Guid.NewGuid();
        var boardId = Guid.NewGuid();
        var config = JsonSerializer.Serialize(TournamentConfigurationJsonEntity.From(
            new TournamentConfiguration(boardId, "frozen", new ScoringConfiguration(), false, true)
            {
                MaxTime = TimeSpan.FromMinutes(105),
                AllowRepeats = false,
                StartDate = startsAt ?? SeasonStart,
                EndDate = endsAt ?? SeasonEnd
            }));
        await using var ctx = await _fixture.DbContextFactory.CreateDbContextAsync();
        await ctx.Database.ExecuteSqlRawAsync(
            "IF NOT EXISTS (SELECT 1 FROM scores.MoMSeason WHERE Id = {0}) " +
            "INSERT INTO scores.MoMSeason (Id, [Year], Quarter, Name, StartsAt, EndsAt, CreatedAt) " +
            "VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {4}); " +
            "INSERT INTO scores.MoMBoard (Id, SeasonId, MixId, ChartType, ScoringConfig) " +
            "VALUES ({6}, {0}, {7}, {8}, {9});",
            seasonId, (object?)year!, (object?)quarter!, seasonName,
            startsAt ?? SeasonStart, endsAt ?? SeasonEnd, boardId, MixIds.For(mix), (byte)chartType, config);
        return (seasonId, boardId);
    }

    private async Task SeedBoardOnSeason(Guid seasonId, Guid boardId, MixEnum mix, ChartType chartType)
    {
        await using var ctx = await _fixture.DbContextFactory.CreateDbContextAsync();
        await ctx.Database.ExecuteSqlRawAsync(
            "INSERT INTO scores.MoMBoard (Id, SeasonId, MixId, ChartType, ScoringConfig) " +
            "SELECT {0}, {1}, {2}, {3}, ScoringConfig FROM scores.MoMBoard WHERE SeasonId = {1}",
            boardId, seasonId, MixIds.For(mix), (byte)chartType);
    }

    private static Chart BuildChart(Guid chartId, MixEnum mix, int level = 20, ChartType type = ChartType.Single)
    {
        var song = new Song($"song_{chartId:N}", SongType.Arcade,
            new Uri("https://example.invalid/song.png"), TimeSpan.FromMinutes(2), "Artist", null);
        return new Chart(chartId, mix, song, type, DifficultyLevel.From(level), mix,
            null, null, new HashSet<Skill>());
    }

    private static Mock<IChartRepository> ChartRepoReturning(MixEnum mix, params Chart[] charts)
    {
        var chartRepo = new Mock<IChartRepository>();
        chartRepo.Setup(c => c.GetCharts(mix, null, null, It.IsAny<IEnumerable<Guid>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(charts);
        return chartRepo;
    }

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
            "Onsite", true, null, null, null, false), CancellationToken.None);

        var tournaments = (await BuildRepository().GetAllTournaments(CancellationToken.None)).ToList();
        Assert.Single(tournaments);
        Assert.Equal("New", (string)tournaments[0].Name);
        Assert.Equal("Onsite", tournaments[0].Location);
        Assert.True(tournaments[0].IsHighlighted);
    }

    [Fact]
    public async Task GetAllTournamentsListsBoardsAndHidesTheCopiedLegacyMoMRows()
    {
        // A legacy MoM row on the old table: copied to the MoM* tables by the migration, so
        // listing it again would show every season twice.
        await BuildRepository().CreateOrSaveTournament(new TournamentRecord(Guid.NewGuid(),
            "March of Murlocs Legacy - Doubles", 0, TournamentType.Stamina, "Remote", false,
            null, null, null, IsMoM: true), CancellationToken.None);

        // A quarterly two-board season and an off-grid single-board one — the display names
        // must reconstruct exactly what the legacy rows carried.
        var (seasonId, singles) = await SeedBoard(MixEnum.Phoenix, ChartType.Single, "Winter 2025");
        var doubles = Guid.NewGuid();
        await SeedBoardOnSeason(seasonId, doubles, MixEnum.Phoenix, ChartType.Double);
        var (_, practice) = await SeedBoard(MixEnum.Phoenix, ChartType.Double,
            "March of Murlocs Practice", year: null, quarter: null);

        var tournaments = (await BuildRepository().GetAllTournaments(CancellationToken.None)).ToList();

        Assert.Equal(3, tournaments.Count);
        Assert.DoesNotContain(tournaments, t => (string)t.Name == "March of Murlocs Legacy - Doubles");
        var singlesRecord = Assert.Single(tournaments, t => t.Id == singles);
        Assert.Equal("March of Murlocs Winter 2025 - Singles", (string)singlesRecord.Name);
        Assert.True(singlesRecord.IsMoM);
        // Now (2026-08-14) is inside the seeded window, so the board is the highlighted one.
        Assert.True(singlesRecord.IsHighlighted);
        Assert.Equal("March of Murlocs Winter 2025 - Doubles",
            (string)Assert.Single(tournaments, t => t.Id == doubles).Name);
        // A single-board off-grid season never carried a chart-type suffix.
        Assert.Equal("March of Murlocs Practice",
            (string)Assert.Single(tournaments, t => t.Id == practice).Name);
    }

    [Fact]
    public async Task GetTournamentForABoardCarriesTheFixedRulesAndSeasonDates()
    {
        var (_, boardId) = await SeedBoard(MixEnum.Phoenix, ChartType.Double);

        var configuration = await BuildRepository().GetTournament(boardId, CancellationToken.None);

        Assert.True(configuration.IsMom);
        Assert.True(configuration.IsHighlighted);
        Assert.Equal(TimeSpan.FromMinutes(105), configuration.MaxTime);
        Assert.False(configuration.AllowRepeats);
        Assert.Equal(SeasonStart, configuration.StartDate);
        Assert.Equal(SeasonEnd, configuration.EndDate);
        Assert.Equal(MixEnum.Phoenix, configuration.Scoring.Mix);
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
}
