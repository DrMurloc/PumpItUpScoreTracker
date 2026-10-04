using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using ScoreTracker.Catalog.Application;
using ScoreTracker.Catalog.Contracts;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.Data.DevTooling;
using ScoreTracker.Data.Persistence;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Tests.Integration.Fixtures;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace ScoreTracker.Tests.Integration;

/// <summary>
///     The local setup a contributor actually runs: a database the migrations just built, then
///     /Dev/Populate filling it from api/v2.
///     <para>
///         The live site is a WireMock server answering in the published wire shapes. Its mix list
///         is the site's own, every mix with its real name and display name, so whatever the mix
///         list carries meets the real column widths of a real migrated schema. The database is
///         never reset first, so the mix rows the catalog lands against are the ones the migrations
///         seeded and nothing else.
///     </para>
/// </summary>
[Collection(IntegrationTestCollection.Name)]
[ExcludeFromCodeCoverage]
public sealed class DevPopulateTests : IDisposable
{
    private const string Token = "local-dev-token";

    // A request this specific outranks the empty answer every mix gets by default.
    private const int CursorPriority = 1;
    private const int DataPriority = 2;
    private const int DefaultPriority = 100;

    private static readonly string[] TierLists = { "score-difficulty", "pass-difficulty", "pg-difficulty" };
    private static readonly string[] PhoenixOnlyTierLists = { "score-difficulty", "pg-difficulty" };

    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web);

    private static readonly Guid SingleChart = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid DoubleChart = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid LegacyChart = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private readonly SqlServerFixture _fixture;
    private readonly WireMockServer _site = WireMockServer.Start();

    public DevPopulateTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
    }

    public void Dispose()
    {
        _site.Stop();
    }

    [Fact]
    public async Task AFreshlyMigratedDatabaseTakesACatalogSpanningEveryMix()
    {
        var database = await _fixture.CreateMigratedDatabaseAsync();
        var mixes = await new GetMixesHandler().Handle(new GetMixesQuery(), CancellationToken.None);
        Assert.Equal(Enum.GetValues<MixEnum>().Order(), mixes.Select(m => m.Mix).Order());
        ServeTheSite(mixes);
        var migratedMixes = await MixRows(database);
        var localUser = Guid.NewGuid();
        var progress = new List<string>();
        using var cache = new MemoryCache(new MemoryCacheOptions());

        var seeder = new DevEnvironmentSeeder(database,
            Options.Create(new ProdSyncConfiguration { BaseUrl = _site.Url + "/" }), cache);
        await seeder.PopulateFromApi(Token, localUser, progress.Add);

        Assert.Equal("Done.", progress[^1]);
        Assert.Equal(2, await Count(database, "SELECT COUNT(*) FROM scores.Song"));
        Assert.Equal(3, await Count(database, "SELECT COUNT(*) FROM scores.Chart"));
        // The single in Phoenix 2 and on the Arcade Station, the double from the second page, and
        // the 1st Dance Floor chart.
        Assert.Equal(4, await Count(database, "SELECT COUNT(*) FROM scores.ChartMix"));
        Assert.Equal(1, await Count(database,
            $"SELECT COUNT(*) FROM scores.ChartMix WHERE ChartId='{DoubleChart}' AND MixId='{MixIds.Phoenix2}'"));
        Assert.Equal(1, await Count(database,
            $"SELECT COUNT(*) FROM scores.ChartMix WHERE ChartId='{SingleChart}' AND MixId='{MixIds.For(MixEnum.RiseArcade)}'"));
        Assert.Equal(1, await Count(database,
            $"SELECT COUNT(*) FROM scores.ChartMix WHERE ChartId='{LegacyChart}' AND MixId='{MixIds.For(MixEnum.FirstDanceFloor)}' AND LegacySlot='Crazy'"));
        Assert.Equal(1, await Count(database,
            $"SELECT COUNT(*) FROM scores.ChartMix cm JOIN scores.MixVersion v ON v.Id = cm.AddedInVersionId WHERE cm.ChartId='{SingleChart}' AND v.Name='1.00.0'"));
        Assert.Equal(1, await Count(database, "SELECT COUNT(*) FROM scores.TierListEntry WHERE TierListName='Scores'"));
        Assert.Equal(2, await Count(database, $"SELECT COUNT(*) FROM scores.PhoenixRecord WHERE UserId='{localUser}'"));
        Assert.Equal(migratedMixes, await MixRows(database));
    }

    /// <summary>
    ///     Every route the harness reads, for every mix the site lists. A mix answers with an empty
    ///     page unless the catalog below gives it rows, and a Phoenix-only tier list answers a
    ///     legacy mix with the 404 the real controller gives it.
    /// </summary>
    private void ServeTheSite(IReadOnlyList<MixRecord> mixes)
    {
        Serve("/api/v2/mixes", DefaultPriority, Page(mixes.Select(m => (object)new
        {
            name = m.Name,
            displayName = m.DisplayName,
            sortOrder = m.SortOrder,
            isPrimary = m.IsPrimary,
            scoringModel = m.UsesLegacyScoring ? "legacy" : "phoenix"
        }).ToArray()));

        foreach (var mix in mixes)
        {
            Serve("/api/v2/versions", DefaultPriority, Page(), mix.Name);
            Serve("/api/v2/songs", DefaultPriority, Page(), mix.Name);
            Serve("/api/v2/charts", DefaultPriority, Page(), mix.Name);
            foreach (var list in TierLists)
                if (mix.UsesLegacyScoring && PhoenixOnlyTierLists.Contains(list))
                    NotFound($"/api/v2/tier-lists/{list}", mix.Name);
                else
                    Serve($"/api/v2/tier-lists/{list}", DefaultPriority, Page(), mix.Name);
            Serve("/api/v2/players/me/scores", DefaultPriority, ScorePage(mix), mix.Name);
        }

        // Phoenix 2: one patch, one song, two charts over two pages, a tier list and a score.
        Serve("/api/v2/versions", DataPriority, Page(new object[]
        {
            new { name = "1.00.0", releaseDate = "2026-07-09", sortOrder = 10, chartCount = 2, debutCount = 2 }
        }), "Phoenix2");
        Serve("/api/v2/songs", DataPriority, Page(new[] { BadApple() }), "Phoenix2");
        Serve("/api/v2/charts", DataPriority, Page(new[]
        {
            Chart(SingleChart, MixEnum.Phoenix2, MixEnum.Phoenix2, "Bad Apple!!", "Single", 22, 1200, "1.00.0")
        }, $"{_site.Url}/api/v2/charts?mix=Phoenix2&cursor=page-2"), "Phoenix2");
        Serve("/api/v2/charts", CursorPriority, Page(new[]
        {
            Chart(DoubleChart, MixEnum.Phoenix2, MixEnum.Phoenix2, "Bad Apple!!", "Double", 24, 1500, "1.00.0")
        }), "Phoenix2", ("cursor", "page-2"));
        Serve("/api/v2/tier-lists/score-difficulty", DataPriority, Page(new object[]
        {
            new { chartId = SingleChart, category = "Medium", order = 1 }
        }), "Phoenix2");
        Serve("/api/v2/players/me/scores", DataPriority,
            ScorePage(mixes.Single(m => m.Mix == MixEnum.Phoenix2), Score(SingleChart, 987_654)), "Phoenix2");

        // The Arcade Station plays Phoenix 2's charts, so the same chart id arrives a second time.
        Serve("/api/v2/songs", DataPriority, Page(new[] { BadApple() }), "RiseArcade");
        Serve("/api/v2/charts", DataPriority, Page(new[]
        {
            Chart(SingleChart, MixEnum.RiseArcade, MixEnum.Phoenix2, "Bad Apple!!", "Single", 22, 1200)
        }), "RiseArcade");
        Serve("/api/v2/players/me/scores", DataPriority,
            ScorePage(mixes.Single(m => m.Mix == MixEnum.RiseArcade), Score(SingleChart, 990_100)), "RiseArcade");

        // The first mix: a legacy slot rather than a level, no note count, no step artist.
        Serve("/api/v2/songs", DataPriority, Page(new object[]
        {
            new
            {
                name = "Ignition Starts", type = "Arcade", channel = (string?)null, artist = "BanYa",
                durationSeconds = 100, imageUrl = "https://piu.test/ignition-starts.png", bpm = (object?)null
            }
        }), "FirstDanceFloor");
        Serve("/api/v2/charts", DataPriority, Page(new[]
        {
            Chart(LegacyChart, MixEnum.FirstDanceFloor, MixEnum.FirstDanceFloor, "Ignition Starts", "Single", 7,
                null, legacySlot: "Crazy")
        }), "FirstDanceFloor");
    }

    private void Serve(string path, int priority, string body, string? mix = null,
        params (string Key, string Value)[] query)
    {
        var request = Authorized(path, mix);
        foreach (var (key, value) in query) request = request.WithParam(key, value);

        _site.Given(request).AtPriority(priority).RespondWith(Response.Create()
            .WithStatusCode(200)
            .WithHeader("Content-Type", "application/json")
            .WithBody(body));
    }

    private void NotFound(string path, string mix)
    {
        _site.Given(Authorized(path, mix)).AtPriority(DefaultPriority).RespondWith(Response.Create()
            .WithStatusCode(404)
            .WithHeader("Content-Type", "application/problem+json")
            .WithBody(JsonSerializer.Serialize(new { status = 404, title = "Not Found" }, Wire)));
    }

    /// <summary>
    ///     Only a request carrying the personal token in the scheme a partner tool uses is answered;
    ///     anything else falls through to WireMock's own 404.
    /// </summary>
    private static IRequestBuilder Authorized(string path, string? mix)
    {
        var request = Request.Create().WithPath(path).UsingGet().WithHeader("Authorization",
            "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes($"dev:{Token}")));
        return mix is null ? request : request.WithParam("mix", mix);
    }

    private static string Page(object[]? data = null, string? next = null)
    {
        data ??= Array.Empty<object>();
        return JsonSerializer.Serialize(new { data, limit = 100, total = data.Length, next }, Wire);
    }

    private static string ScorePage(MixRecord mix, params object[] data)
    {
        return JsonSerializer.Serialize(new
        {
            mix = mix.Name,
            scoringModel = mix.UsesLegacyScoring ? "legacy" : "phoenix",
            data,
            limit = 100,
            next = (string?)null
        }, Wire);
    }

    private static object BadApple()
    {
        return new
        {
            name = "Bad Apple!!", type = "Arcade", channel = "Original", artist = "Alstroemeria Records",
            durationSeconds = 105, imageUrl = "https://piu.test/bad-apple.png", bpm = new { min = 138m, max = 138m }
        };
    }

    private static object Chart(Guid id, MixEnum mix, MixEnum originalMix, string song, string type, int level,
        int? noteCount, string? addedInVersion = null, string? legacySlot = null)
    {
        return new
        {
            id,
            mix = mix.ToString(),
            originalMix = originalMix.ToString(),
            addedInVersion,
            addedOn = (string?)null,
            debutVersion = mix == originalMix ? addedInVersion : null,
            debutedOn = (string?)null,
            debut = mix == originalMix,
            channel = legacySlot is null ? "Original" : null,
            songName = song,
            imageUrl = "https://piu.test/chart.png",
            type,
            level,
            difficulty = $"{type[0]}{level}",
            noteCount,
            playerCount = 1,
            stepArtist = legacySlot is null ? "SUNNY" : null,
            legacySlot,
            scoringLevel = (double?)null
        };
    }

    private static object Score(Guid chartId, int score)
    {
        return new
        {
            chartId,
            recordedAt = new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero),
            source = "officialImport",
            score,
            letterGrade = "SS",
            plate = "Marvelous Game",
            isBroken = false,
            pumbility = 1234.5,
            judgments = new { perfects = 1180, greats = 15, goods = 3, bads = 1, misses = 1, maxCombo = 1150 }
        };
    }

    private static async Task<IReadOnlyList<(Guid Id, string Name, int SortOrder, bool IsPrimary)>> MixRows(
        IDbContextFactory<ChartAttemptDbContext> database)
    {
        await using var context = await database.CreateDbContextAsync();
        return (await context.Mix.AsNoTracking().ToListAsync())
            .Select(m => (m.Id, m.Name, m.SortOrder, m.IsPrimary))
            .OrderBy(m => m.Id)
            .ToArray();
    }

    private static async Task<int> Count(IDbContextFactory<ChartAttemptDbContext> database, string sql)
    {
        await using var context = await database.CreateDbContextAsync();
        var connection = (SqlConnection)context.Database.GetDbConnection();
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }
}
