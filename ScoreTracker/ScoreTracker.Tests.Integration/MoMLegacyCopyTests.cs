using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using ScoreTracker.Data.Migrations;
using ScoreTracker.Data.Persistence;
using ScoreTracker.EventCompetition.Infrastructure;
using ScoreTracker.EventCompetition.Infrastructure.Entities;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Tests.Integration.Fixtures;
using ScoreTracker.Tests.Integration.TestData;
using ChartType = ScoreTracker.SharedKernel.Enums.ChartType;

namespace ScoreTracker.Tests.Integration;

/// <summary>
///     Runs the exact copy script the MoMTables migration executes
///     (<see cref="MoMTables.LegacyCopySql" />) against seeded legacy shapes and asserts the
///     §7 mapping (docs/design/march-of-murlocs.md): Singles/Doubles pairs collapse into one
///     season, the quarterly derivation, board Guids preserved, the frozen config copied
///     verbatim, the delta-only snapshot, the JSON blob exploded with old bonus-less entries
///     coalescing to zero, the derived cache columns, and the junk signature excluded. The
///     migration itself runs this script against the fixture's empty legacy tables (a no-op),
///     which is why the test can run it again: it is idempotent by construction, and the last
///     assert proves that too.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
[ExcludeFromCodeCoverage]
public sealed class MoMLegacyCopyTests : IAsyncLifetime
{
    private static readonly DateTimeOffset PairStart = new(2024, 6, 8, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset PairEnd = new(2024, 8, 8, 0, 0, 0, TimeSpan.Zero);

    // The last moment of a quarter-final month in UTC-5 — the shape the season cycle writes,
    // and what the copy must recognize as quarterly (Winter => Year 2025, Quarter 1).
    private static readonly DateTimeOffset WinterStart = new(2025, 2, 2, 0, 0, 0, TimeSpan.FromHours(-5));
    private static readonly DateTimeOffset WinterEnd = new(2025, 3, 31, 23, 59, 59, TimeSpan.FromHours(-5));

    private readonly SqlServerFixture _fixture;

    public MoMLegacyCopyTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task CopiesLegacySeasonsBoardsAndSessionsExactlyOnce()
    {
        var seeder = new TestDataSeeder(_fixture.DbContextFactory);
        var userId = await seeder.SeedUserAsync();
        // Folder levels 20 / 15 / 22. chartA gets a real snapshot override (21.5 = delta),
        // chartB none (falls back to 15.5), chartC exactly level + 0.5 (equivalent to no row,
        // so the copy must NOT store it).
        var chartA = await seeder.SeedPhoenixChartAsync(20, "Double");
        var chartB = await seeder.SeedPhoenixChartAsync(15, "Single");
        var chartC = await seeder.SeedPhoenixChartAsync(22, "Double");

        var singlesId = Guid.NewGuid();
        var doublesId = Guid.NewGuid();
        var soloId = Guid.NewGuid();
        var winterId = Guid.NewGuid();
        var junkId = Guid.NewGuid();
        // A real config in the exact shape production rows carry — serialized through the same
        // DTO — so the read-back half of this test exercises the true deserialization path.
        var config = JsonSerializer.Serialize(TournamentConfigurationJsonEntity.From(
            new TournamentConfiguration(doublesId, "legacy", new ScoringConfiguration(), false, true)
            {
                MaxTime = TimeSpan.FromMinutes(105),
                AllowRepeats = false,
                StartDate = PairStart,
                EndDate = PairEnd
            }));

        await SeedTournament(singlesId, "Copy Test 2 - Singles", config, PairStart, PairEnd);
        await SeedTournament(doublesId, "Copy Test 2 - Doubles", config, PairStart, PairEnd);
        await SeedTournament(soloId, "Copy Test Solo", config, PairStart, PairEnd.AddDays(1));
        await SeedTournament(winterId, "Copy Test Winter - Doubles", config, WinterStart, WinterEnd);
        // The junk signature (§9.1): inverted dates. Must produce no season and no board.
        await SeedTournament(junkId, "Copy Test Junk", config, PairEnd, PairStart);

        // Session on the Doubles board whose entries include one Singles chart — the 2023
        // stray play, copied faithfully. chartA is new-format JSON (BonusPoints present),
        // chartB predates the field. 990,000 = SSS (ordinal 14), 960,000 = AAA+ (ordinal 9).
        var sessionId = Guid.NewGuid();
        var entries =
            $@"[{{""ChartId"":""{chartA}"",""Score"":990000,""SessionScore"":1500,""Plate"":""SuperbGame"",""IsBroken"":false,""BonusPoints"":25}}," +
            $@"{{""ChartId"":""{chartB}"",""Score"":960000,""SessionScore"":800,""Plate"":""FairGame"",""IsBroken"":true}}]";
        await using (var ctx = await _fixture.DbContextFactory.CreateDbContextAsync())
        {
            await ctx.Database.ExecuteSqlRawAsync(
                "INSERT INTO scores.UserTournamentSession (Id, UserId, TournamentId, MixId, SessionScore, " +
                "ChartEntries, RestTime, AverageDifficulty, ChartsPlayed, VideoUrl, VerificationType, NeedsApproval) " +
                "VALUES ({0}, {1}, {2}, {3}, 2300, {4}, {5}, 17.5, 2, {6}, 'Unverified', 0)",
                sessionId, userId, doublesId, TestDataSeeder.PhoenixMixId, entries,
                new TimeSpan(0, 13, 43), "https://example.invalid/v");

            await ctx.Database.ExecuteSqlRawAsync(
                "INSERT INTO scores.TournamentChartLevel (Id, TournamentId, ChartId, Level) VALUES " +
                "({0}, {1}, {2}, 21.5), ({3}, {1}, {4}, 22.5)",
                Guid.NewGuid(), doublesId, chartA, Guid.NewGuid(), chartC);

            await ctx.Database.ExecuteSqlRawAsync(MoMTables.LegacyCopySql);
        }

        // Seasons: the pair collapses into one, the junk row vanishes, and only Winter is
        // quarterly — three seasons for five seeded tournaments.
        Assert.Equal(3, await Scalar<int>("SELECT COUNT(*) AS Value FROM scores.MoMSeason"));
        Assert.Equal(1, await Scalar<int>(
            "SELECT COUNT(*) AS Value FROM scores.MoMSeason WHERE Name = 'Copy Test 2'"));
        Assert.Equal(1, await Scalar<int>(
            "SELECT COUNT(*) AS Value FROM scores.MoMSeason WHERE Name = 'Copy Test Winter' " +
            "AND [Year] = 2025 AND Quarter = 1"));
        Assert.Equal(2, await Scalar<int>(
            "SELECT COUNT(*) AS Value FROM scores.MoMSeason WHERE [Year] IS NULL AND Quarter IS NULL"));

        // Boards: legacy tournament Guids preserved, chart types from the name suffix (a
        // suffix-less name is a Doubles board), the config byte-identical, junk absent.
        Assert.Equal(4, await Scalar<int>("SELECT COUNT(*) AS Value FROM scores.MoMBoard"));
        Assert.Equal(0, await Scalar<int>(
            $"SELECT COUNT(*) AS Value FROM scores.MoMBoard WHERE Id = '{junkId}'"));
        Assert.Equal(1, await Scalar<int>(
            $"SELECT COUNT(*) AS Value FROM scores.MoMBoard WHERE Id = '{singlesId}' AND ChartType = 0"));
        Assert.Equal(1, await Scalar<int>(
            $"SELECT COUNT(*) AS Value FROM scores.MoMBoard WHERE Id = '{soloId}' AND ChartType = 1"));
        // Parameterized: the JSON's braces read as format holes if inlined — SqlQueryRaw
        // composite-formats its SQL just like ExecuteSqlRawAsync.
        Assert.Equal(4, await Scalar<int>(
            "SELECT COUNT(*) AS Value FROM scores.MoMBoard WHERE ScoringConfig = {0}", config));

        // The copy → read seam: the rows the script just produced must reconstruct through
        // the live repository — plate strings parse, ordinals hold, the frozen config
        // deserializes, and the stored total survives as the board's number.
        var domainChartA = BuildChart(chartA, 20, ChartType.Double);
        var domainChartB = BuildChart(chartB, 15, ChartType.Single);
        var chartRepo = new Mock<IChartRepository>();
        chartRepo.Setup(c => c.GetCharts(MixEnum.Phoenix, null, null, It.IsAny<IEnumerable<Guid>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { domainChartA, domainChartB });
        var repository = new EFTournamentRepository(new MemoryCache(new MemoryCacheOptions()),
            chartRepo.Object, _fixture.DbContextFactory, Mock.Of<ICurrentUserAccessor>(),
            Mock.Of<IDateTimeOffsetAccessor>(d => d.Now == PairEnd));

        // Read back through the side that owns MoM reads: the copied rows are what matter,
        // not which port hands them over.
        var read = new EFMoMReadRepository(_fixture.DbContextFactory);
        var copied = Assert.Single((await read.GetPublishedSessions(new[] { doublesId },
            CancellationToken.None)).Where(s => s.UserId == userId));
        var rows = (await read.GetSessionCharts(new[] { copied.Id }, CancellationToken.None))
            .OrderBy(r => r.Ordinal).ToArray();
        Assert.Equal(2, rows.Length);
        Assert.Equal(chartA, rows[0].ChartId);
        Assert.Equal((PhoenixScore)990000, rows[0].Score);
        Assert.Equal(PhoenixPlate.SuperbGame, rows[0].Plate);
        Assert.Equal(chartB, rows[1].ChartId);
        Assert.True(rows[1].IsBroken);
        Assert.Equal("https://example.invalid/v", copied.VideoUrl?.ToString());

        var board = (await repository.GetLeaderboardRecords(doublesId, CancellationToken.None)).ToArray();
        var placement = Assert.Single(board);
        Assert.Equal(1, placement.Place);
        Assert.Equal(userId, placement.UserId);
        Assert.Equal(2300, placement.TotalScore);
        Assert.Equal(2, placement.Session.Entries.Count);

        // The session: published at its season's end, rest time in ticks (13:43 = 823s),
        // total preserved, and the derived cache computed over the snapshot — balanced
        // (21.5 + 15.5) / 2 = 18.5, grades (14 + 9) / 2 = 11.5, folder min/max 15/20.
        Assert.Equal(1, await Scalar<int>(
            "SELECT COUNT(*) AS Value FROM scores.MoMSession m " +
            "JOIN scores.MoMBoard b ON b.Id = m.BoardId " +
            "JOIN scores.MoMSeason s ON s.Id = b.SeasonId " +
            $"WHERE m.Id = '{sessionId}' AND m.BoardId = '{doublesId}' AND m.PublishedAt = s.EndsAt " +
            "AND m.TotalScore = 2300 AND m.ChartsPlayed = 2 AND m.RestTime = 8230000000 " +
            "AND m.AverageDifficulty = 18.5 AND m.AverageGrade = 11.5 " +
            "AND m.LowestLevel = 15 AND m.HighestLevel = 20 " +
            "AND m.VideoUrl = 'https://example.invalid/v'"));

        // The blob explodes in order; the bonus-less entry coalesces to zero.
        Assert.Equal(1, await Scalar<int>(
            "SELECT COUNT(*) AS Value FROM scores.MoMSessionChart " +
            $"WHERE SessionId = '{sessionId}' AND Ordinal = 0 AND ChartId = '{chartA}' " +
            "AND Score = 990000 AND SessionScore = 1500 AND Plate = 'SuperbGame' " +
            "AND IsBroken = 0 AND BonusPoints = 25 AND PlayedAt IS NULL"));
        Assert.Equal(1, await Scalar<int>(
            "SELECT COUNT(*) AS Value FROM scores.MoMSessionChart " +
            $"WHERE SessionId = '{sessionId}' AND Ordinal = 1 AND ChartId = '{chartB}' " +
            "AND IsBroken = 1 AND BonusPoints = 0"));

        // Delta-only snapshot: the real override copies, the level + 0.5 row does not (§9.3).
        Assert.Equal(1, await Scalar<int>(
            $"SELECT COUNT(*) AS Value FROM scores.MoMChartLevel WHERE ChartId = '{chartA}' AND Level = 21.5"));
        Assert.Equal(0, await Scalar<int>(
            $"SELECT COUNT(*) AS Value FROM scores.MoMChartLevel WHERE ChartId = '{chartC}'"));

        // Idempotency: a second full run moves nothing.
        await using (var ctx = await _fixture.DbContextFactory.CreateDbContextAsync())
        {
            await ctx.Database.ExecuteSqlRawAsync(MoMTables.LegacyCopySql);
        }

        Assert.Equal(3, await Scalar<int>("SELECT COUNT(*) AS Value FROM scores.MoMSeason"));
        Assert.Equal(4, await Scalar<int>("SELECT COUNT(*) AS Value FROM scores.MoMBoard"));
        Assert.Equal(1, await Scalar<int>("SELECT COUNT(*) AS Value FROM scores.MoMSession"));
        Assert.Equal(2, await Scalar<int>("SELECT COUNT(*) AS Value FROM scores.MoMSessionChart"));
        Assert.Equal(1, await Scalar<int>("SELECT COUNT(*) AS Value FROM scores.MoMChartLevel"));
    }

    private static Chart BuildChart(Guid chartId, int level, ChartType type)
    {
        var song = new Song($"song_{chartId:N}", SongType.Arcade,
            new Uri("https://example.invalid/song.png"), TimeSpan.FromMinutes(2), "Artist", null);
        return new Chart(chartId, MixEnum.Phoenix, song, type, DifficultyLevel.From(level),
            MixEnum.Phoenix, null, null);
    }

    private async Task SeedTournament(Guid id, string name, string configuration,
        DateTimeOffset start, DateTimeOffset end)
    {
        await using var ctx = await _fixture.DbContextFactory.CreateDbContextAsync();
        await ctx.Database.ExecuteSqlRawAsync(
            "INSERT INTO scores.Tournament (Id, Name, Configuration, StartDate, EndDate, " +
            "IsHighlighted, IsMoM, IsUnlisted, Type, Location) " +
            "VALUES ({0}, {1}, {2}, {3}, {4}, 0, 1, 0, 'Stamina', 'Remote')",
            id, name, configuration, start, end);
    }

    private async Task<T> Scalar<T>(string sql, params object[] parameters)
    {
        await using var ctx = await _fixture.DbContextFactory.CreateDbContextAsync();
        return await ctx.Database.SqlQueryRaw<T>(sql, parameters).SingleAsync();
    }
}
