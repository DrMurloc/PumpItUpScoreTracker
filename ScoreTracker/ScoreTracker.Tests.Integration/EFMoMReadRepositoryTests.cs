using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Domain.Models;
using ScoreTracker.EventCompetition.Infrastructure;
using ScoreTracker.EventCompetition.Infrastructure.Entities;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.Integration.Fixtures;
using ScoreTracker.Tests.Integration.TestData;
using ChartType = ScoreTracker.SharedKernel.Enums.ChartType;

namespace ScoreTracker.Tests.Integration;

/// <summary>
///     The MoM read side against a real migrated database (docs/design/march-of-murlocs.md
///     §12.2): seasons newest first, boards with their frozen configuration over the season's
///     delta-only snapshot, published sessions without the drafts, and chart rows in ordinal order.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
[ExcludeFromCodeCoverage]
public sealed class EFMoMReadRepositoryTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Start = new(2025, 2, 1, 0, 0, 0, TimeSpan.Zero);
    private readonly SqlServerFixture _fixture;

    public EFMoMReadRepositoryTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private EFMoMReadRepository Repo() => new(_fixture.DbContextFactory);

    private async Task<Guid> SeedSeason(string name, DateTimeOffset start, int? year = 2025, byte? quarter = 1)
    {
        var id = Guid.NewGuid();
        await using var ctx = await _fixture.DbContextFactory.CreateDbContextAsync();
        await ctx.Database.ExecuteSqlRawAsync(
            "INSERT INTO scores.MoMSeason (Id, [Year], Quarter, Name, StartsAt, EndsAt, CreatedAt) VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {4})",
            id, (object?)year!, (object?)quarter!, name, start, start.AddMonths(2));
        return id;
    }

    private async Task<Guid> SeedBoard(Guid seasonId, ChartType type, int level24Rating = 1450)
    {
        var id = Guid.NewGuid();
        var scoring = ScoringConfiguration.PumbilityPlus;
        scoring.AdjustToTime = true;
        scoring.LevelRatings[DifficultyLevel.From(24)] = level24Rating;
        var json = JsonSerializer.Serialize(TournamentConfigurationJsonEntity.From(
            new TournamentConfiguration(id, "frozen", scoring, false, true)
                { MaxTime = TimeSpan.FromMinutes(105), AllowRepeats = false }));
        await using var ctx = await _fixture.DbContextFactory.CreateDbContextAsync();
        await ctx.Database.ExecuteSqlRawAsync(
            "INSERT INTO scores.MoMBoard (Id, SeasonId, MixId, ChartType, ScoringConfig) VALUES ({0}, {1}, {2}, {3}, {4})",
            id, seasonId, MixIds.Phoenix, (byte)type, json);
        return id;
    }

    private async Task<Guid> SeedSession(Guid boardId, Guid userId, int total, DateTimeOffset? publishedAt,
        long restTicks = 0, string? video = null)
    {
        var id = Guid.NewGuid();
        await using var ctx = await _fixture.DbContextFactory.CreateDbContextAsync();
        await ctx.Database.ExecuteSqlRawAsync(
            "INSERT INTO scores.MoMSession (Id, BoardId, UserId, PublishedAt, TotalScore, ChartsPlayed, RestTime, AverageDifficulty, AverageGrade, LowestLevel, HighestLevel, VideoUrl, CreatedAt, UpdatedAt) " +
            "VALUES ({0}, {1}, {2}, {3}, {4}, 2, {5}, 24.25, 8.5, 23, 25, {6}, {7}, {7})",
            id, boardId, userId, (object?)publishedAt!, total, restTicks, (object?)video!, Start);
        return id;
    }

    private async Task SeedRow(Guid sessionId, int ordinal, Guid chartId, int score, string plate, int points)
    {
        await using var ctx = await _fixture.DbContextFactory.CreateDbContextAsync();
        await ctx.Database.ExecuteSqlRawAsync(
            "INSERT INTO scores.MoMSessionChart (SessionId, Ordinal, ChartId, Score, Plate, IsBroken, SessionScore, BonusPoints, PlayedAt) VALUES ({0}, {1}, {2}, {3}, {4}, 0, {5}, 0, NULL)",
            sessionId, ordinal, chartId, score, plate, points);
    }

    [Fact]
    public async Task SeasonsComeNewestFirst()
    {
        var older = await SeedSeason("March of Murlocs 2", Start.AddMonths(-8), null, null);
        var newer = await SeedSeason("Winter 2025", Start);

        var seasons = await Repo().GetSeasons(CancellationToken.None);

        Assert.Equal(new[] { newer, older }, seasons.Select(s => s.Id));
        Assert.Equal("Winter 2025", seasons[0].Name);
        Assert.Equal(2025, seasons[0].Year);
        Assert.Equal((byte)1, seasons[0].Quarter);
        Assert.Null(seasons[1].Quarter);
        Assert.Equal(Start.AddMonths(2), seasons[0].EndsAt);
    }

    [Fact]
    public async Task BoardsCarryTheFrozenConfigurationOverTheSeasonsSnapshot()
    {
        var seeder = new TestDataSeeder(_fixture.DbContextFactory);
        var season = await SeedSeason("Winter 2025", Start);
        var doubles = await SeedBoard(season, ChartType.Double);
        var singles = await SeedBoard(season, ChartType.Single);
        var chart = await seeder.SeedPhoenixChartAsync(24, "Double");
        await using (var ctx = await _fixture.DbContextFactory.CreateDbContextAsync())
        {
            await ctx.Database.ExecuteSqlRawAsync(
                "INSERT INTO scores.MoMChartLevel (SeasonId, MixId, ChartId, Level) VALUES ({0}, {1}, {2}, 24.9)",
                season, MixIds.Phoenix, chart);
        }

        var boards = await Repo().GetBoards(new[] { season }, CancellationToken.None);
        var one = await Repo().GetBoard(doubles, CancellationToken.None);

        Assert.Equal(2, boards.Count);
        var board = boards.Single(b => b.Id == doubles);
        Assert.Equal(ChartType.Double, board.ChartType);
        Assert.Equal(MixEnum.Phoenix, board.Mix);
        Assert.Equal(season, board.SeasonId);
        Assert.Equal(TimeSpan.FromMinutes(105), board.Configuration.MaxTime);
        Assert.False(board.Configuration.AllowRepeats);
        Assert.Equal(1450, board.Configuration.Scoring.LevelRatings[DifficultyLevel.From(24)]);
        Assert.True(board.Configuration.Scoring.AdjustToTime);
        Assert.Equal(24.9, board.Configuration.Scoring.ChartLevelSnapshot![chart]);
        Assert.Equal(MixEnum.Phoenix, board.Configuration.Scoring.Mix);
        Assert.Equal("March of Murlocs Winter 2025 - Doubles", board.Configuration.Name.ToString());
        Assert.Equal(ChartType.Single, boards.Single(b => b.Id == singles).ChartType);
        Assert.Equal(doubles, one!.Id);
        Assert.Equal(24.9, one.Configuration.Scoring.ChartLevelSnapshot![chart]);
        Assert.Null(await Repo().GetBoard(Guid.NewGuid(), CancellationToken.None));
        Assert.Empty(await Repo().GetBoards(Array.Empty<Guid>(), CancellationToken.None));
    }

    [Fact]
    public async Task PublishedSessionsLeaveTheDraftsBehindAndCarryTheDerivedColumns()
    {
        var seeder = new TestDataSeeder(_fixture.DbContextFactory);
        var season = await SeedSeason("Winter 2025", Start);
        var board = await SeedBoard(season, ChartType.Double);
        var otherBoard = await SeedBoard(season, ChartType.Single);
        var kim = await seeder.SeedUserAsync();
        var drafter = await seeder.SeedUserAsync();
        var published = await SeedSession(board, kim, 59319, Start.AddDays(13), TimeSpan.FromSeconds(1324).Ticks, "https://youtu.be/VPx-aEAneJE");
        var draft = await SeedSession(board, drafter, 70000, null);
        await SeedSession(otherBoard, kim, 42596, Start.AddDays(5));

        var sessions = await Repo().GetPublishedSessions(new[] { board }, CancellationToken.None);
        var one = await Repo().GetSession(draft, CancellationToken.None);

        var row = Assert.Single(sessions);
        Assert.Equal(published, row.Id);
        Assert.Equal(kim, row.UserId);
        Assert.Equal(59319, row.TotalScore);
        Assert.Equal(TimeSpan.FromSeconds(1324), row.Downtime);
        Assert.Equal(24.25, row.AverageDifficulty);
        Assert.Equal(new Uri("https://youtu.be/VPx-aEAneJE"), row.VideoUrl);
        Assert.Equal(Start.AddDays(13), row.PublishedAt);
        Assert.NotNull(one);
        Assert.Null(one!.PublishedAt);
        Assert.Equal(70000, one.TotalScore);
        Assert.Null(await Repo().GetSession(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task SessionChartsComeBackInOrdinalOrderForTheRequestedSessionsOnly()
    {
        var seeder = new TestDataSeeder(_fixture.DbContextFactory);
        var season = await SeedSeason("Winter 2025", Start);
        var board = await SeedBoard(season, ChartType.Double);
        var user = await seeder.SeedUserAsync();
        var mine = await SeedSession(board, user, 3000, Start.AddDays(1));
        var theirs = await SeedSession(board, await seeder.SeedUserAsync(), 2000, Start.AddDays(2));
        var slam = await seeder.SeedPhoenixChartAsync(24, "Double");
        var odin = await seeder.SeedPhoenixChartAsync(23, "Double");
        await SeedRow(mine, 1, odin, 976240, "TalentedGame", 1400);
        await SeedRow(mine, 0, slam, 976489, "FairGame", 1600);
        await SeedRow(theirs, 0, slam, 900000, "RoughGame", 700);

        var rows = await Repo().GetSessionCharts(new[] { mine }, CancellationToken.None);

        Assert.Equal(2, rows.Count);
        Assert.Equal(new[] { 0, 1 }, rows.Select(r => r.Ordinal));
        Assert.Equal(slam, rows[0].ChartId);
        Assert.Equal(976489, (int)rows[0].Score);
        Assert.Equal(PhoenixPlate.FairGame, rows[0].Plate);
        Assert.Equal(1600, rows[0].SessionScore);
        Assert.Null(rows[0].PlayedAt);
        Assert.Equal(PhoenixPlate.TalentedGame, rows[1].Plate);
        Assert.Empty(await Repo().GetSessionCharts(Array.Empty<Guid>(), CancellationToken.None));
    }
}
