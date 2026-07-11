using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using ScoreTracker.Data.Apis;
using ScoreTracker.Data.Configuration;
using ScoreTracker.SharedKernel.Enums;
using Xunit;

namespace ScoreTracker.Tests.ApprovalTests;

/// <summary>
///     Approval tests for the piucenter data client. Fixtures are real captures of the
///     site's static JSON data files (release 050726) plus the SPA app shell and the
///     data-*.js bundle the version is resolved from. These catch piucenter data-shape
///     drift — a renamed field, a restructured segment block, a new key suffix.
/// </summary>
public sealed class PiuCenterApiTests
{
    private static readonly string FixtureRoot = Path.Combine(
        AppContext.BaseDirectory, "ApprovalTests", "PiuCenterApi", "Fixtures");

    private static string Fixture(string name)
    {
        return File.ReadAllText(Path.Combine(FixtureRoot, name));
    }

    /// <summary>
    ///     Routing stub for piucenter's static host: requests are answered by the first
    ///     route whose fragment appears in the URL, and everything else gets the SPA app
    ///     shell with HTTP 200 — exactly how the real host behaves for unknown files.
    /// </summary>
    private static PiuCenterApi BuildApi(List<Uri>? requests = null,
        params (string UrlContains, string Body)[] routes)
    {
        var shell = Fixture("app-shell.html");
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns((HttpRequestMessage request, CancellationToken _) =>
            {
                requests?.Add(request.RequestUri!);
                var url = request.RequestUri!.AbsoluteUri;
                var body = routes.Where(r => url.Contains(r.UrlContains)).Select(r => r.Body).FirstOrDefault()
                           ?? (url.Contains("/_build/assets/data-") ? Fixture("data-bundle.js") : shell);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(body, Encoding.UTF8)
                });
            });
        return new PiuCenterApi(new HttpClient(handler.Object), Options.Create(new PiuCenterConfiguration()),
            NullLogger<PiuCenterApi>.Instance);
    }

    [Fact]
    public async Task ChartTableParsesRowsAcrossAllPacksAndKeyVariants()
    {
        var api = BuildApi(routes: ("page-content/chart-table.json", Fixture("chart-table.sample.json")));

        var table = await api.GetChartTable(CancellationToken.None);

        // The sample was cut to cover every pack and every key-suffix variant on the
        // live table; parsing must not drop a single row of it.
        Assert.Equal(20, table.Count);
        Assert.Equal(13, table.Select(l => l.Pack).Distinct().Count());
        foreach (var variant in new[] { "ARCADE", "REMIX", "SHORTCUT", "FULLSONG", "HALFDOUBLE_ARCADE" })
            Assert.Contains(table, l => l.Variant == variant);

        var slam = table.Single(l => l.ExternalKey == "Slam_-_Novasonic_S7_ARCADE");
        Assert.Equal(ChartType.Single, slam.Type);
        Assert.Equal(7, slam.Level);
        Assert.Equal("S.E.~EXTRA", slam.Pack);
        Assert.Equal(new[] { "jump", "doublestep", "twists" }, slam.TopSkills);
        Assert.Equal(4.4m, slam.Nps);
        Assert.Equal("8th notes @ 132 bpm", slam.BpmInfo);
        Assert.Equal(5m, slam.SustainTime);
        Assert.Equal(5m, slam.TimeUnderTension);

        var tribe = table.Single(l => l.ExternalKey == "Tribe_Attacker_-_Hi-G_D10_HALFDOUBLE_ARCADE");
        Assert.Equal(ChartType.Double, tribe.Type);
        Assert.Equal(10, tribe.Level);
        Assert.Equal("HALFDOUBLE_ARCADE", tribe.Variant);
    }

    [Fact]
    public async Task ChartPageParsesSkillSummaryAndSegmentBadgeTallies()
    {
        var api = BuildApi(routes: ("Repentance_-_Abel_D20_ARCADE.json", Fixture("chart-page_Repentance_D20.json")));

        var page = await api.GetChartPage("Repentance_-_Abel_D20_ARCADE", CancellationToken.None);

        Assert.NotNull(page);
        // Their dominance pick — the top-3 skill summary.
        Assert.Equal(new[] { "bracket_drill", "bracket_run", "bracket" }, page!.SkillSummary);
        // Per-segment badge tallies: the fixture has 8 segments, twist_90 badged on 4.
        Assert.Equal(8, page.SegmentCount);
        Assert.Equal(4, page.SegmentSkillCounts["twist_90"]);
        Assert.Equal(3, page.SegmentSkillCounts["bracket_jump"]);
        Assert.Equal(2, page.RareSkillCounts["bracket drill-5"]);
        Assert.Equal(12.0m, page.Nps);
        Assert.Equal("D20", page.SordChartLevel);
    }

    [Fact]
    public async Task MissingChartPageReturnsNullWhenTheShellComesBack()
    {
        // Static host quirk: unknown files return HTTP 200 with the SPA shell. The
        // client must sniff content, not status codes.
        var api = BuildApi();

        var page = await api.GetChartPage("Definitely_Not_A_Real_Chart_-_Nobody_S99_ARCADE",
            CancellationToken.None);

        Assert.Null(page);
    }

    [Fact]
    public async Task PracticeListsParseRankedEntriesPerSkillAndLevel()
    {
        var api = BuildApi(routes: ("page-content/stepchart-skills.json", Fixture("stepchart-skills.sample.json")));

        var entries = await api.GetPracticeLists(CancellationToken.None);

        // Sample carries two skills at two levels each; descriptions (element 1 of the
        // file) must not leak in as entries.
        Assert.Equal(2, entries.Select(e => e.Skill).Distinct().Count());
        var jumpS7 = entries.Where(e => e is { Skill: "jump", SordLevel: "S7" }).OrderBy(e => e.Rank).ToArray();
        Assert.Equal(20, jumpS7.Length);
        Assert.Equal(1, jumpS7[0].Rank);
        Assert.Equal("Slam_-_Novasonic_S7_ARCADE", jumpS7[0].ExternalKey);
    }

    [Fact]
    public async Task DifficultyPredictionsFlattenFolderClusters()
    {
        var api = BuildApi(routes: ("page-content/tierlists.json", Fixture("tierlists.sample.json")));

        var predictions = await api.GetDifficultyPredictions(CancellationToken.None);

        // D10's first cluster pairs keys with numeric predictions positionally.
        Assert.Equal(11.11m, predictions["Kill_Them!_-_Archefluxx_D10_ARCADE"]);
        Assert.True(predictions.Count > 20);
    }

    [Fact]
    public async Task DataVersionIsResolvedFromTheAppShellAndDataBundle()
    {
        // The chart-jsons version is hardcoded in the site's data-*.js bundle; the
        // client must discover it by reading the shell, following the bundle reference,
        // and only then requesting data files under the resolved version.
        var requests = new List<Uri>();
        var api = BuildApi(requests, ("page-content/chart-table.json", Fixture("chart-table.sample.json")));

        await api.GetChartTable(CancellationToken.None);

        Assert.Contains(requests, u => u.AbsolutePath == "/");
        Assert.Contains(requests, u => u.AbsolutePath.Contains("/_build/assets/data-"));
        Assert.Contains(requests, u => u.AbsolutePath.Contains("/chart-jsons/050726/page-content/chart-table.json"));
    }
}
