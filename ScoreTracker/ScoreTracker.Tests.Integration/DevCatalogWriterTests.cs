using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.DevTooling;
using ScoreTracker.Data.Persistence;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Tests.Integration.Fixtures;

namespace ScoreTracker.Tests.Integration;

/// <summary>
///     The local-dev harness's writer, against a real database.
///     <para>
///         This is the acceptance test for retiring <c>dev/export</c>: if a catalog assembled from
///         the public API's wire shapes can be written and then read back through the ordinary
///         repositories, the public surface is complete enough to develop against and the raw table
///         export has nothing left to do.
///     </para>
/// </summary>
[Collection(IntegrationTestCollection.Name)]
[ExcludeFromCodeCoverage]
public sealed class DevCatalogWriterTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 8, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid ChartId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherChartId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly SqlServerFixture _fixture;

    public DevCatalogWriterTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync()
    {
        return _fixture.ResetAsync();
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    private DevCatalogWriter BuildSeeder()
    {
        return new DevCatalogWriter(_fixture.DbContextFactory);
    }

    private static DevCatalogSnapshot Snapshot(params DevChartRow[] extraCharts)
    {
        var charts = new List<DevChartRow>
        {
            new(ChartId, MixEnum.Phoenix, MixEnum.Phoenix, "Bad Apple!!", "Single", 21, 1200, 1,
                "SUNNY", null),
            // Same chart in a second mix, at a different level — the shape ChartMix exists for.
            new(ChartId, MixEnum.Phoenix2, MixEnum.Phoenix, "Bad Apple!!", "Single", 22, 1200, 1,
                "SUNNY", null)
        };
        charts.AddRange(extraCharts);

        return new DevCatalogSnapshot(
            new[]
            {
                new DevMixRow(MixEnum.Phoenix, "Phoenix", 27, true),
                new DevMixRow(MixEnum.Phoenix2, "Phoenix2", 28, true)
            },
            new[]
            {
                new DevSongRow("Bad Apple!!", "Arcade", "Alstroemeria Records", 105,
                    "https://piu.test/badapple.png", 138, 138)
            },
            charts,
            new[] { new DevTierListRow("Scores", MixEnum.Phoenix, ChartId, "Medium", 3) },
            new[] { new DevScoringLevelRow(MixEnum.Phoenix, ChartId, 21.4) });
    }

    [Fact]
    public async Task ACatalogBuiltFromWireShapesLandsInEveryTable()
    {
        await BuildSeeder().ReplaceCatalog(Snapshot());

        Assert.Equal(2, await CountOf("Mix"));
        Assert.Equal(1, await CountOf("Song"));
        // One Chart row for the id, one ChartMix row per mix it exists in.
        Assert.Equal(1, await CountOf("Chart"));
        Assert.Equal(2, await CountOf("ChartMix"));
        Assert.Equal(1, await CountOf("TierListEntry"));
        Assert.Equal(1, await CountOf("ChartScoringLevel"));
    }

    /// <summary>
    ///     Levels are per-mix, and a harness that flattened them would make every Phoenix 2 chart
    ///     read at its Phoenix level — the exact defect the ChartMix table exists to prevent.
    /// </summary>
    [Fact]
    public async Task EachMixKeepsItsOwnLevelForTheSameChart()
    {
        await BuildSeeder().ReplaceCatalog(Snapshot());

        Assert.Equal(21, Convert.ToInt32(await Scalar(
            $"SELECT Level FROM scores.ChartMix WHERE ChartId='{ChartId}' AND MixId='{MixIds.Phoenix}'")));
        Assert.Equal(22, Convert.ToInt32(await Scalar(
            $"SELECT Level FROM scores.ChartMix WHERE ChartId='{ChartId}' AND MixId='{MixIds.Phoenix2}'")));
    }

    [Fact]
    public async Task SongDetailsSurviveTheRoundTrip()
    {
        await BuildSeeder().ReplaceCatalog(Snapshot());

        Assert.Equal("Bad Apple!!", await Scalar("SELECT Name FROM scores.Song"));
        Assert.Equal("Alstroemeria Records", await Scalar("SELECT Artist FROM scores.Song"));
        Assert.Equal(TimeSpan.FromSeconds(105).Ticks, await Scalar("SELECT Duration FROM scores.Song"));
        Assert.Equal(138m, await Scalar("SELECT MinBpm FROM scores.Song"));
    }

    /// <summary>
    ///     Repopulating is the normal case — a dev syncs, works, syncs again — and it must leave one
    ///     catalog rather than two.
    /// </summary>
    [Fact]
    public async Task RepopulatingReplacesRatherThanAccumulates()
    {
        var seeder = BuildSeeder();
        await seeder.ReplaceCatalog(Snapshot());
        await seeder.ReplaceCatalog(Snapshot());

        Assert.Equal(1, await CountOf("Chart"));
        Assert.Equal(2, await CountOf("ChartMix"));
        Assert.Equal(1, await CountOf("Song"));
    }

    [Fact]
    public async Task ScoresLandAgainstTheLocalUserAndKeepTheirJudgments()
    {
        var seeder = BuildSeeder();
        var userId = Guid.NewGuid();
        await seeder.ReplaceCatalog(Snapshot());

        await seeder.ReplaceUserScores(userId, new[]
        {
            new DevScoreRow(ChartId, MixEnum.Phoenix, Now, 987_654, "AA", "PerfectGame", false,
                "OfficialImport", 900, 80, 4, 1, 2)
        });

        Assert.Equal(1, await CountOf("PhoenixRecord"));
        Assert.Equal(userId, await Scalar("SELECT UserId FROM scores.PhoenixRecord"));
        Assert.Equal(ChartId, await Scalar("SELECT ChartId FROM scores.PhoenixRecord"));
        Assert.Equal(987_654, await Scalar("SELECT Score FROM scores.PhoenixRecord"));
        Assert.Equal(900, await Scalar("SELECT Perfects FROM scores.PhoenixRecord"));
        Assert.Equal(2, await Scalar("SELECT Misses FROM scores.PhoenixRecord"));
        Assert.Equal(false, await Scalar("SELECT IsBroken FROM scores.PhoenixRecord"));
    }

    /// <summary>
    ///     A score for a chart the catalog does not have would be invisible and would break the
    ///     joins that assume otherwise. Dropping it is the honest outcome of a partial catalog.
    /// </summary>
    [Fact]
    public async Task AScoreForAnUnknownChartIsDroppedRatherThanOrphaned()
    {
        var seeder = BuildSeeder();
        var userId = Guid.NewGuid();
        await seeder.ReplaceCatalog(Snapshot());

        await seeder.ReplaceUserScores(userId, new[]
        {
            new DevScoreRow(ChartId, MixEnum.Phoenix, Now, 900_000, "A+", null, true, null,
                null, null, null, null, null),
            new DevScoreRow(OtherChartId, MixEnum.Phoenix, Now, 950_000, "AA", null, false, null,
                null, null, null, null, null)
        });

        Assert.Equal(1, await CountOf("PhoenixRecord"));
        Assert.Equal(ChartId, await Scalar("SELECT ChartId FROM scores.PhoenixRecord"));
    }

    /// <summary>
    ///     A hand-entered score has no judgment breakdown. Zeros there would read as a perfect game
    ///     everywhere the site renders one.
    /// </summary>
    [Fact]
    public async Task AScoreWithoutJudgmentsStaysNullRatherThanZeroed()
    {
        var seeder = BuildSeeder();
        await seeder.ReplaceCatalog(Snapshot());

        await seeder.ReplaceUserScores(Guid.NewGuid(), new[]
        {
            new DevScoreRow(ChartId, MixEnum.Phoenix, Now, 900_000, "A+", "RoughGame", false, "Manual",
                null, null, null, null, null)
        });

        Assert.Null(await Scalar("SELECT Perfects FROM scores.PhoenixRecord"));
        Assert.Null(await Scalar("SELECT Misses FROM scores.PhoenixRecord"));
    }

    /// <summary>
    ///     Replacing the catalog has to clear the scores that point at the old chart ids first, or
    ///     the delete fails on the foreign key and a dev's second sync never completes.
    /// </summary>
    [Fact]
    public async Task ReplacingTheCatalogClearsScoresThatPointedAtIt()
    {
        var seeder = BuildSeeder();
        await seeder.ReplaceCatalog(Snapshot());
        await seeder.ReplaceUserScores(Guid.NewGuid(), new[]
        {
            new DevScoreRow(ChartId, MixEnum.Phoenix, Now, 900_000, "A+", null, false, null,
                null, null, null, null, null)
        });

        await seeder.ReplaceCatalog(Snapshot());

        Assert.Equal(0, await CountOf("PhoenixRecord"));
    }

    /// <summary>
    ///     Read back with SQL rather than through the context. Half these tables belong to verticals
    ///     and their entities are internal there, and the seeder writes SQL anyway — asserting at the
    ///     same level is what proves the rows are really shaped the way the schema wants.
    /// </summary>
    private async Task<int> CountOf(string table)
    {
        return Convert.ToInt32(await Scalar($"SELECT COUNT(*) FROM scores.[{table}]"));
    }

    private async Task<object?> Scalar(string sql)
    {
        await using var database = await _fixture.DbContextFactory.CreateDbContextAsync();
        var connection = (SqlConnection)database.Database.GetDbConnection();
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        var value = await command.ExecuteScalarAsync();
        return value is DBNull ? null : value;
    }
}
