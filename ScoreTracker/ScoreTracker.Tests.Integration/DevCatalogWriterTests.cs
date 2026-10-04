using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ScoreTracker.Catalog.Infrastructure.Entities;
using ScoreTracker.Data.DevTooling;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Data.Persistence.Entities;
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

    /// <summary>
    ///     The mix rows a migrated database holds, as the migrations seed them: real ids and the
    ///     stored short names, which are not what api/v2 calls a mix. The harness writes none of
    ///     these, so a test that needs one seeds it the way a migration would.
    /// </summary>
    private static readonly MixEntity[] SeededMixes =
    {
        new() { Id = MixIds.Phoenix, Name = "Phoenix", SortOrder = 270, IsPrimary = true },
        new() { Id = MixIds.Phoenix2, Name = "Phoenix2", SortOrder = 280, IsPrimary = true },
        new() { Id = MixIds.For(MixEnum.FirstDanceFloor), Name = "1st", SortOrder = 10, IsPrimary = false },
        new() { Id = MixIds.For(MixEnum.RiseArcade), Name = "RiseArcade", SortOrder = 274, IsPrimary = true }
    };

    public async Task InitializeAsync()
    {
        await _fixture.ResetAsync();
        await SeedMixes(SeededMixes);
    }

    private async Task SeedMixes(IEnumerable<MixEntity> mixes)
    {
        await using var ctx = await _fixture.DbContextFactory.CreateDbContextAsync();
        ctx.Mix.AddRange(mixes.Select(m => new MixEntity
            { Id = m.Id, Name = m.Name, SortOrder = m.SortOrder, IsPrimary = m.IsPrimary }));
        await ctx.SaveChangesAsync();
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

    /// <summary>
    ///     A catalog reaches from the first mix to Rise. Those mixes' display names do not fit the
    ///     mix table's name column, and the harness writes no mix rows, so their charts land against
    ///     the rows the database already holds.
    /// </summary>
    [Fact]
    public async Task ACatalogSpanningLegacyAndRiseMixesLands()
    {
        var legacyChart = Guid.NewGuid();
        var riseChart = Guid.NewGuid();
        var firstDanceFloor = MixIds.For(MixEnum.FirstDanceFloor);
        var riseArcade = MixIds.For(MixEnum.RiseArcade);
        var snapshot = Snapshot(
            new DevChartRow(legacyChart, MixEnum.FirstDanceFloor, MixEnum.FirstDanceFloor, "Bad Apple!!", "Single",
                7, null, 1, null, "Crazy"),
            new DevChartRow(riseChart, MixEnum.RiseArcade, MixEnum.RiseArcade, "Bad Apple!!", "Double",
                18, 900, 1, "SUNNY", null));

        await BuildSeeder().ReplaceCatalog(snapshot);

        Assert.Equal(firstDanceFloor, await Scalar($"SELECT OriginalMixId FROM scores.Chart WHERE Id='{legacyChart}'"));
        Assert.Equal("Crazy", await Scalar(
            $"SELECT LegacySlot FROM scores.ChartMix WHERE ChartId='{legacyChart}' AND MixId='{firstDanceFloor}'"));
        Assert.Equal(18, Convert.ToInt32(await Scalar(
            $"SELECT Level FROM scores.ChartMix WHERE ChartId='{riseChart}' AND MixId='{riseArcade}'")));
    }

    /// <summary>
    ///     The mix rows are the migrations', down to short names nothing on the wire carries. A sync,
    ///     and a second one, leaves every row as it was — including a mix the catalog never mentions.
    /// </summary>
    [Fact]
    public async Task PopulatingNeverRewritesTheMixTable()
    {
        var unmentioned = new MixEntity
            { Id = MixIds.For(MixEnum.Prime2), Name = "Prime 2", SortOrder = 250, IsPrimary = false };
        await SeedMixes(new[] { unmentioned });
        var seeder = BuildSeeder();

        await seeder.ReplaceCatalog(Snapshot());
        await seeder.ReplaceCatalog(Snapshot());

        await using var ctx = await _fixture.DbContextFactory.CreateDbContextAsync();
        var stored = (await ctx.Mix.ToListAsync())
            .Select(m => (m.Id, m.Name, m.SortOrder, m.IsPrimary))
            .OrderBy(m => m.SortOrder);
        var seeded = SeededMixes.Append(unmentioned)
            .Select(m => (m.Id, m.Name, m.SortOrder, m.IsPrimary))
            .OrderBy(m => m.SortOrder);
        Assert.Equal(seeded, stored);
    }

    /// <summary>
    ///     A value wider than its column is refused while the row is staged, before any SQL runs.
    ///     The Populate page prints only the message, so the message carries the table as well as
    ///     the column's own complaint.
    /// </summary>
    [Fact]
    public async Task AValueTooLongForItsColumnNamesTheTable()
    {
        var snapshot = Snapshot() with
        {
            MixVersions = new[] { new DevMixVersionRow(MixEnum.Phoenix2, "1.00.0-hotfix-rc1", null, 10) }
        };

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => BuildSeeder().ReplaceCatalog(snapshot));

        Assert.Contains("[scores].[MixVersion]", thrown.Message);
        Assert.Contains("MaxLength", thrown.Message);
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

    // ── The patches ride the harness like any other catalog fact (docs/design/chart-versions.md §5) ──

    [Fact]
    public async Task VersionsAreWrittenAndAChartRowLinksToItsPatchByName()
    {
        var snapshot = Snapshot(new DevChartRow(Guid.NewGuid(), MixEnum.Phoenix2, MixEnum.Phoenix2, "Bad Apple!!",
            "Double", 24, 1500, 1, "SUNNY", null, "1.01.0")) with
        {
            MixVersions = new[]
            {
                new DevMixVersionRow(MixEnum.Phoenix2, "1.00.0", new DateOnly(2026, 7, 9), 10),
                new DevMixVersionRow(MixEnum.Phoenix2, "1.01.0", new DateOnly(2026, 9, 3), 20)
            }
        };

        await BuildSeeder().ReplaceCatalog(snapshot);

        await using var ctx = await _fixture.DbContextFactory.CreateDbContextAsync();
        var versions = await ctx.Set<MixVersionEntity>().OrderBy(v => v.SortOrder).ToListAsync();
        Assert.Equal(new[] { "1.00.0", "1.01.0" }, versions.Select(v => v.Name));
        Assert.Equal(new DateOnly(2026, 9, 3), versions[1].ReleaseDate);
        var stamped = await ctx.ChartMix.SingleAsync(cm => cm.AddedInVersionId != null);
        Assert.Equal(versions[1].Id, stamped.AddedInVersionId);
        Assert.Equal(24, stamped.Level);
    }

    [Fact]
    public async Task ARowWhoseVersionNameIsUnknownStaysUnstampedRatherThanFailing()
    {
        var snapshot = Snapshot(new DevChartRow(Guid.NewGuid(), MixEnum.Phoenix2, MixEnum.Phoenix2, "Bad Apple!!",
            "Double", 24, 1500, 1, "SUNNY", null, "9.99.9"));

        await BuildSeeder().ReplaceCatalog(snapshot);

        await using var ctx = await _fixture.DbContextFactory.CreateDbContextAsync();
        Assert.Empty(await ctx.Set<MixVersionEntity>().ToListAsync());
        Assert.Equal(0, await ctx.ChartMix.CountAsync(cm => cm.AddedInVersionId != null));
    }

    // ── A song's channel per mix rides the harness too (docs/design/song-channels.md §3) ──

    [Fact]
    public async Task ChannelsLandAsOneSongMixRowPerSongPerMixAndNoneWhereTheWireHasNone()
    {
        var songOfTwoCharts = Guid.NewGuid();
        var snapshot = Snapshot(
            new DevChartRow(songOfTwoCharts, MixEnum.Phoenix2, MixEnum.Phoenix2, "Bad Apple!!", "Single", 15, 800, 1, "SUNNY", null, null, "WorldMusic"),
            new DevChartRow(Guid.NewGuid(), MixEnum.Phoenix2, MixEnum.Phoenix2, "Bad Apple!!", "Double", 24, 1500, 1, "SUNNY", null, null, "WorldMusic"),
            new DevChartRow(Guid.NewGuid(), MixEnum.Phoenix2, MixEnum.Phoenix2, "Mystery", "Single", 18, 900, 1, "EXC", null, null, null));

        await BuildSeeder().ReplaceCatalog(snapshot);

        Assert.Equal(1, await CountOf("SongMix"));
        Assert.Equal("WorldMusic", await Scalar(
            "SELECT sm.Channel FROM scores.SongMix sm JOIN scores.Song s ON s.Id = sm.SongId WHERE s.Name = N'Bad Apple!!'"));
        Assert.Null(await Scalar(
            "SELECT sm.Channel FROM scores.SongMix sm JOIN scores.Song s ON s.Id = sm.SongId WHERE s.Name = N'Mystery'"));
    }
}
