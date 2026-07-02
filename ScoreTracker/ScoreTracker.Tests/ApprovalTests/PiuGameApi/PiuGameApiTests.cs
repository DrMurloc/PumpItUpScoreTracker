using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using ScoreTracker.OfficialMirror.Infrastructure.Apis;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.SecondaryPorts;
using Xunit;

namespace ScoreTracker.Tests.ApprovalTests;

/// <summary>
/// Approval tests for the PIU site HTML parser. Each fixture is a real (PII-scrubbed) HTML capture
/// of a PIU page; the test feeds it through an HttpMessageHandler stub and asserts the parser's
/// output shape. These catch PIU layout drift — the day PIU silently changes a class name or
/// rearranges a structure, these go red.
/// </summary>
public sealed class PiuGameApiTests
{
    private static readonly string FixtureRoot = Path.Combine(
        AppContext.BaseDirectory, "ApprovalTests", "PiuGameApi", "Fixtures");

    [Fact]
    public async Task GetBestScoresParsesScoresAndMaxPageFromHappyPathFixture()
    {
        var html = await File.ReadAllTextAsync(Path.Combine(FixtureRoot, "GetBestScores_HappyPath.html"));
        var api = BuildApi(html);

        var result = await api.GetBestScores(HttpClientReturning(html), page: 1, CancellationToken.None);

        // Pagination — last page button on the fixture is `?&&page=238`.
        Assert.Equal(238, result.MaxPage);

        // Score entries — assert the fixture parses at least one and that the first entry
        // matches the literal values we can see in the captured HTML.
        Assert.NotEmpty(result.Scores);
        var first = result.Scores.First();
        Assert.Equal("TRICKL4SH 220", (string)first.SongName);
        Assert.Equal(ChartType.Double, first.ChartType);
        Assert.Equal(20, (int)first.Level);
        Assert.Equal(999231, (int)first.Score);
        Assert.Equal(PhoenixPlate.ExtremeGame, first.Plate);

        // Second entry pins that the parser advances through the list AND handles different
        // chart types / plates (Single vs Double, TalentedGame vs ExtremeGame).
        var second = result.Scores.Skip(1).First();
        Assert.Equal("Conflict", (string)second.SongName);
        Assert.Equal(ChartType.Single, second.ChartType);
        Assert.Equal(15, (int)second.Level);
        Assert.Equal(850000, (int)second.Score);
        Assert.Equal(PhoenixPlate.TalentedGame, second.Plate);
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("pt-BR")]
    [InlineData("fr-FR")]
    [InlineData("it-IT")]
    public async Task GetRecentScoresParsesAllValidEntriesAcrossCultures(string cultureName)
    {
        // PIU formats note counts with "," as the thousand separator (e.g. "1,144"). Before the
        // 2026-05 fix, int.Parse used the thread's current culture — so requests from non-en-US
        // users threw FormatException on cards with 1,000+ note counts and the entry was silently
        // dropped by the per-card try/catch. This theory pins the fix: every supported culture
        // parses the fixture identically. Without the fix, the three non-en-US cases would each
        // return only 1 entry (the first card, which has no comma-formatted counts) instead of 2.
        var previousCulture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
        try
        {
            var html = await File.ReadAllTextAsync(Path.Combine(FixtureRoot, "GetRecentScores_HappyPath.html"));
            var stubbedClient = HttpClientReturning(html);
            var api = BuildApi(html);

            var result = (await api.GetRecentScores(stubbedClient, CancellationToken.None)).ToList();

            // The fixture has 3 cards; card 2 is STAGE BREAK and is auto-skipped by the parser.
            Assert.Equal(2, result.Count);

            // First parsed entry — TRICKL4SH 220, Double 20, broken stage (no plate image present).
            var first = result[0];
            Assert.Equal("TRICKL4SH 220", (string)first.SongName);
            Assert.Equal(ChartType.Double, first.ChartType);
            Assert.Equal(20, (int)first.Level);
            Assert.Equal(940078, (int)first.Score);
            Assert.Equal(1042, first.NoteCount); // 974 + 8 + 3 + 2 + 55
            Assert.True(first.IsBroken);

            // Second parsed entry — Appassionata Double 21, PERFECT=1,144 (the bug-trigger value).
            var second = result[1];
            Assert.Equal("Appassionata", (string)second.SongName);
            Assert.Equal(ChartType.Double, second.ChartType);
            Assert.Equal(21, (int)second.Level);
            Assert.Equal(965679, (int)second.Score);
            Assert.Equal(1200, second.NoteCount); // 1144 + 23 + 11 + 9 + 13
            Assert.False(second.IsBroken);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    [Fact]
    public async Task GetRecentScoresPreservesKoreanSongNamesFromLocalizedFixture()
    {
        // PIU's content language varies by session — if our scraper picks up a Korean session
        // (cookie, account language preference, Accept-Language sniffing), song names come back
        // as Korean transliterations. The parser must preserve the raw Korean text; downstream
        // `OfficialSiteClient.GetMappedName` then maps it to the canonical English name via the
        // `SongNameLanguage` table. This test locks the parser side of that contract — without
        // a critical mass of Korean users to surface regressions organically, we rely on this
        // approval test to catch breakage when someone refactors string handling.
        var html = await File.ReadAllTextAsync(Path.Combine(FixtureRoot, "GetRecentScores_Korean.html"));
        var stubbedClient = HttpClientReturning(html);
        var api = BuildApi(html);

        var result = (await api.GetRecentScores(stubbedClient, CancellationToken.None)).ToList();

        // Same 3 cards as the English fixture; STAGE BREAK card is auto-skipped → 2 entries.
        Assert.Equal(2, result.Count);

        // Korean song name preserved verbatim.
        Assert.Equal("트릭크래쉬 220", (string)result[0].SongName);
        Assert.Equal("열정", (string)result[1].SongName);

        // Numbers, chart types, and pagination markers don't depend on language — same values
        // as the English fixture parses to.
        Assert.Equal(ChartType.Double, result[0].ChartType);
        Assert.Equal(20, (int)result[0].Level);
        Assert.Equal(940078, (int)result[0].Score);
        Assert.Equal(1042, result[0].NoteCount);
        Assert.True(result[0].IsBroken);

        Assert.Equal(ChartType.Double, result[1].ChartType);
        Assert.Equal(21, (int)result[1].Level);
        Assert.Equal(965679, (int)result[1].Score);
        Assert.Equal(1200, result[1].NoteCount);
        Assert.False(result[1].IsBroken);
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("pt-BR")]
    [InlineData("fr-FR")]
    [InlineData("it-IT")]
    public async Task GetSongLeaderboardParsesEntriesAcrossCultures(string cultureName)
    {
        // Same culture-sensitivity as GetRecentScores: leaderboard scores come back with "," as
        // thousand separator. The line-133 fix added InvariantCulture; this theory pins it.
        // Unlike GetRecentScores, this method is called from Hangfire recurring-job threads in
        // production (which default to en-US on Azure App Service), so the bug hadn't manifested
        // in the wild — but it was latent and would have fired the day Hangfire's thread culture
        // changed or this method got called from a request-context thread.
        var previousCulture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
        try
        {
            var html = await File.ReadAllTextAsync(Path.Combine(FixtureRoot, "GetSongLeaderboard_HappyPath.html"));
            var stubbedClient = HttpClientReturning(html);
            var api = BuildApi(html);

            var result = await api.GetSongLeaderboard(songId: "any", CancellationToken.None);

            Assert.Equal(2, result.Results.Length);

            // ProfileName is the concatenation of every `profile_name` div in the entry — for PIU
            // that's the gamer tag followed by the #ID suffix.
            Assert.Equal("Player1#0001", result.Results[0].ProfileName);
            Assert.Equal(987436, result.Results[0].Score);
            Assert.Equal(
                new Uri("https://piugame.com/data/avatar_img/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.png?v=20250923184201"),
                result.Results[0].AvatarUrl);

            Assert.Equal("Player2#0002", result.Results[1].ProfileName);
            Assert.Equal(986895, result.Results[1].Score);
            Assert.Equal(
                new Uri("https://piugame.com/data/avatar_img/bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb.png?v=20250923184201"),
                result.Results[1].AvatarUrl);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    [Fact]
    public async Task GetLeaderboardsParsesAllAvailableRatingLeaderboardOptionsFromDropdown()
    {
        // The page's <select> dropdown lists every available rating leaderboard PIU offers.
        // GetLeaderboards extracts these as (Id, Name) pairs — consumed by the scheduled scrape
        // saga to know which leaderboards to iterate.
        var html = await File.ReadAllTextAsync(Path.Combine(FixtureRoot, "GetLeaderboard_HappyPath.html"));
        var stubbedClient = HttpClientReturning(html);
        var api = BuildApi(html);

        var result = await api.GetLeaderboards(CancellationToken.None);

        // PIU offers: All + LEVEL 10..26 (17 levels) + LEVEL 27 OVER + LEVEL 10 OVER + CO-OP = 21.
        Assert.Equal(21, result.Entries.Length);
        Assert.Equal("All", result.Entries[0].Name);
        Assert.Equal("", result.Entries[0].Id);
        Assert.Equal("LEVEL 10", result.Entries[1].Name);
        Assert.Equal("10", result.Entries[1].Id);
        Assert.Equal("LEVEL 26", result.Entries[17].Name);
        Assert.Equal("26", result.Entries[17].Id);
        Assert.Equal("CO-OP", result.Entries[20].Name);
        Assert.Equal("coop", result.Entries[20].Id);
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("pt-BR")]
    [InlineData("fr-FR")]
    [InlineData("it-IT")]
    public async Task GetLeaderboardParsesRatingsAcrossCultures(string cultureName)
    {
        // Line-190 fix coverage. Ratings on this leaderboard are 7-digit numbers (e.g. "3,088,301").
        // Any value with a thousands comma trips culture-sensitive int.Parse without InvariantCulture.
        // Unlike user-facing GetRecentScores, GetLeaderboard runs on Hangfire recurring-job threads
        // in production (which default to en-US on Azure), so the bug hadn't manifested in App
        // Insights — but it was latent and would fire if Hangfire culture ever changed or this
        // method got called from a request-context thread.
        var previousCulture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
        try
        {
            var html = await File.ReadAllTextAsync(Path.Combine(FixtureRoot, "GetLeaderboard_HappyPath.html"));
            var stubbedClient = HttpClientReturning(html);
            var api = BuildApi(html);

            var result = await api.GetLeaderboard(leaderboardId: "any", CancellationToken.None);

            Assert.Equal(2, result.Entries.Length);
            Assert.Equal("Player1#0001", result.Entries[0].ProfileName);
            Assert.Equal(3088301, result.Entries[0].Rating);
            Assert.Equal("Player2#0002", result.Entries[1].ProfileName);
            Assert.Equal(3086069, result.Entries[1].Rating);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    private static PiuGameApi BuildApi(string responseHtml)
    {
        return new PiuGameApi(
            HttpClientReturning(responseHtml),
            NullLogger<PiuGameApi>.Instance,
            Mock.Of<ICurrentUserAccessor>());
    }

    private static HttpClient HttpClientReturning(string html)
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(html, Encoding.UTF8, "text/html")
            });
        return new HttpClient(handler.Object);
    }
}
