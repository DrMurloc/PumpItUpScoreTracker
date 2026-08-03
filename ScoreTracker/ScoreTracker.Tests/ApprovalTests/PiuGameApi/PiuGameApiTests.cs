using System;
using System.Collections.Generic;
using System.Globalization;
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
using ScoreTracker.OfficialMirror.Infrastructure.Apis;
using ScoreTracker.OfficialMirror.Wiring;
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

        var result = await api.GetBestScores(MixEnum.Phoenix, HttpClientReturning(html), page: 1,
            CancellationToken.None);

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

    [Fact]
    public async Task GetBestScoresParsesIdenticallyWhenImagesComeFromThePhoenixHost()
    {
        // 2026-07-03 incident: with the Phoenix 2 site rollout, PIU moved the stepball/plate
        // images from piugame.com to phoenix.piugame.com. GetBestScores was still slicing the
        // level digit and plate shorthand out of the src by fixed character offset, so every
        // user's import failed with FormatException ("The input string 'll' was not in a
        // correct format" — offset 46 landed on 'full' instead of the digit). This fixture is
        // the happy-path capture with every asset URL on the new host; it must parse to the
        // exact same values.
        var html = await File.ReadAllTextAsync(Path.Combine(FixtureRoot, "GetBestScores_PhoenixHost.html"));
        var api = BuildApi(html);

        var result = await api.GetBestScores(MixEnum.Phoenix, HttpClientReturning(html), page: 1,
            CancellationToken.None);

        Assert.Equal(238, result.MaxPage);
        Assert.NotEmpty(result.Scores);

        var first = result.Scores.First();
        Assert.Equal("TRICKL4SH 220", (string)first.SongName);
        Assert.Equal(ChartType.Double, first.ChartType);
        Assert.Equal(20, (int)first.Level);
        Assert.Equal(999231, (int)first.Score);
        Assert.Equal(PhoenixPlate.ExtremeGame, first.Plate);

        var second = result.Scores.Skip(1).First();
        Assert.Equal("Conflict", (string)second.SongName);
        Assert.Equal(ChartType.Single, second.ChartType);
        Assert.Equal(15, (int)second.Level);
        Assert.Equal(850000, (int)second.Score);
        Assert.Equal(PhoenixPlate.TalentedGame, second.Plate);
    }

    [Fact]
    public async Task GetBestScoresParsesTheRedesignedPhoenix2PageWithDatesAndBrokenBests()
    {
        // Phoenix 2 redesigned my_best_score.php (captured 2026-07-17): the my_best_scoreList
        // grammar is gone; bests render as recently-played-style cards, newest first, each
        // carrying a saved datetime — and stage-failed bests appear with no plate image and a
        // real (not necessarily 0) partial score. The parser sniffs the shape, so the classic
        // fixtures above must keep parsing unchanged through the same entry point.
        var html = await File.ReadAllTextAsync(Path.Combine(FixtureRoot, "GetBestScores_Phoenix2Redesign.html"));
        var api = BuildApi(html);

        var result = await api.GetBestScores(MixEnum.Phoenix2, HttpClientReturning(html), page: 1,
            CancellationToken.None);

        // No pager on the fixture — MaxPage falls back to the requested page.
        Assert.Equal(1, result.MaxPage);
        Assert.Equal(5, result.Scores.Length);

        // Card 1: a stage-failed best — empty plate slot, still scored and dated.
        var broken = result.Scores[0];
        Assert.Equal("Chimera", (string)broken.SongName);
        Assert.Equal(ChartType.Double, broken.ChartType);
        Assert.Equal(26, (int)broken.Level);
        Assert.Equal(0, (int)broken.Score);
        Assert.True(broken.IsBroken);
        Assert.Null(broken.Plate);
        Assert.Equal(new DateTimeOffset(2026, 7, 17, 23, 16, 30, TimeSpan.FromHours(9)), broken.RecordedAt);

        // Card 2: a passing best — plate parsed from its image, grade imagery ignored.
        var pass = result.Scores[1];
        Assert.Equal("ALiVE", (string)pass.SongName);
        Assert.Equal(ChartType.Double, pass.ChartType);
        Assert.Equal(21, (int)pass.Level);
        Assert.Equal(978147, (int)pass.Score);
        Assert.False(pass.IsBroken);
        Assert.Equal(PhoenixPlate.FairGame, pass.Plate);
        Assert.Equal(new DateTimeOffset(2026, 7, 17, 23, 15, 58, TimeSpan.FromHours(9)), pass.RecordedAt);

        // Strictly newest-first — the incremental import's date cutoff depends on this ordering.
        Assert.Equal(result.Scores.Select(s => s.RecordedAt!.Value).OrderByDescending(d => d),
            result.Scores.Select(s => s.RecordedAt!.Value));
    }

    [Fact]
    public async Task GetRecentScoresParsesJudgementCountsAndSavedDates()
    {
        // Both sites now stamp recently-played cards with a saved datetime; the judgement
        // counts were always on the card and now ride the result instead of being discarded
        // after the plate computation.
        var html = await File.ReadAllTextAsync(Path.Combine(FixtureRoot, "GetRecentScores_Phoenix2Dated.html"));
        var api = BuildApi(html);

        var result = (await api.GetRecentScores(MixEnum.Phoenix2, HttpClientReturning(html), CancellationToken.None))
            .ToList();

        // 5 cards; the STAGE BREAK card is skipped as always (broken data arrives via the
        // best list on the redesigned site).
        Assert.Equal(4, result.Count);

        var first = result[0];
        Assert.Equal("ALiVE", (string)first.SongName);
        Assert.Equal(978147, (int)first.Score);
        Assert.Equal(1100, first.Perfects);
        Assert.Equal(14, first.Greats);
        Assert.Equal(1, first.Goods);
        Assert.Equal(1, first.Bads);
        Assert.Equal(14, first.Misses);
        Assert.Equal(1130, first.NoteCount);
        Assert.Equal(PhoenixPlate.FairGame, first.Plate);
        Assert.Equal(new DateTimeOffset(2026, 7, 17, 23, 15, 58, TimeSpan.FromHours(9)), first.RecordedAt);
    }

    [Fact]
    public async Task GetRecentScoresParsesSavedDatesAndJudgementsOnTheClassicCapture()
    {
        // The Phoenix 1 site has stamped recently-played cards with recently_date_tt since at
        // least this 2026-05 capture — the classic shape carries dates and judgements too,
        // so capture works identically on both hosts.
        var html = await File.ReadAllTextAsync(Path.Combine(FixtureRoot, "GetRecentScores_HappyPath.html"));
        var api = BuildApi(html);

        var result = (await api.GetRecentScores(MixEnum.Phoenix, HttpClientReturning(html), CancellationToken.None))
            .ToList();

        Assert.NotEmpty(result);
        Assert.All(result, r => Assert.NotNull(r.RecordedAt));
        Assert.Equal(new DateTimeOffset(2026, 5, 6, 5, 3, 46, TimeSpan.FromHours(9)), result[0].RecordedAt);
        Assert.Equal(974, result[0].Perfects);
        Assert.Equal(55, result[0].Misses);
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

            var result = (await api.GetRecentScores(MixEnum.Phoenix, stubbedClient, CancellationToken.None)).ToList();

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

        var result = (await api.GetRecentScores(MixEnum.Phoenix, stubbedClient, CancellationToken.None)).ToList();

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

            var result = await api.GetSongLeaderboard(MixEnum.Phoenix, songId: "any", page: 1,
                CancellationToken.None);

            Assert.Equal(2, result.Results.Length);
            // No next/last paging icon in the fixture — the whole board is one page.
            Assert.True(result.IsEnd);
            Assert.Equal(0, result.FailedRows);

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

        var result = await api.GetLeaderboards(MixEnum.Phoenix, CancellationToken.None);

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

            var result = await api.GetLeaderboard(MixEnum.Phoenix, leaderboardId: "any", CancellationToken.None);

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

    [Fact]
    public async Task GetRecentScoresParsesPhoenix2StepballPathsIdentically()
    {
        // The Phoenix 2 site serves stepball images from /l_img/p2/stepball/full/ (extra
        // "p2" segment, live recon 2026-07-04). The ANCHORED LevelRegex/TypeRegex used to
        // reject that path, sending every P2 card to the SinglePerformance fallback with a
        // fallback level. The fixture is the happy-path capture rewritten onto the P2 paths;
        // it must parse to the exact same chart types and levels.
        var html = await File.ReadAllTextAsync(Path.Combine(FixtureRoot, "GetRecentScores_Phoenix2Host.html"));
        var stubbedClient = HttpClientReturning(html);
        var api = BuildApi(html);

        var result = (await api.GetRecentScores(MixEnum.Phoenix2, stubbedClient, CancellationToken.None)).ToList();

        // Same 3 cards as the happy path; STAGE BREAK card auto-skipped → 2 entries.
        Assert.Equal(2, result.Count);

        Assert.Equal("TRICKL4SH 220", (string)result[0].SongName);
        Assert.Equal(ChartType.Double, result[0].ChartType);
        Assert.Equal(20, (int)result[0].Level);
        Assert.Equal(940078, (int)result[0].Score);
        Assert.True(result[0].IsBroken);

        Assert.Equal("Appassionata", (string)result[1].SongName);
        Assert.Equal(ChartType.Double, result[1].ChartType);
        Assert.Equal(21, (int)result[1].Level);
        Assert.Equal(965679, (int)result[1].Score);
        Assert.False(result[1].IsBroken);
    }

    [Fact]
    public async Task GetBestScoresParsesPhoenix2PathsAndReadsTheUnknownLevelStepballAs29()
    {
        // Two P2-specific behaviors in one fixture (the PhoenixHost capture rewritten onto
        // piugame.com + /p2/ stepball paths):
        //  1. Chart TYPE comes from the anchored TypeRegex — pre-fix, the p2 segment made
        //     every card fall back to SinglePerformance.
        //  2. The second card is the "??" stepball (1948 D29 renders no parseable digit
        //     images on P2): the joined level digits come back empty, which used to
        //     int.Parse-throw and silently drop the card. Owner-confirmed ?? == 29.
        var html = await File.ReadAllTextAsync(Path.Combine(FixtureRoot, "GetBestScores_Phoenix2Host.html"));
        var api = BuildApi(html);

        var result = await api.GetBestScores(MixEnum.Phoenix2, HttpClientReturning(html), page: 1,
            CancellationToken.None);

        Assert.Equal(238, result.MaxPage);
        Assert.Equal(2, result.Scores.Length);

        var first = result.Scores.First();
        Assert.Equal("TRICKL4SH 220", (string)first.SongName);
        Assert.Equal(ChartType.Double, first.ChartType);
        Assert.Equal(20, (int)first.Level);
        Assert.Equal(999231, (int)first.Score);
        Assert.Equal(PhoenixPlate.ExtremeGame, first.Plate);

        // The ?? card is KEPT and read as a 29, not dropped.
        var second = result.Scores.Skip(1).First();
        Assert.Equal("Conflict", (string)second.SongName);
        Assert.Equal(ChartType.Single, second.ChartType);
        Assert.Equal(29, (int)second.Level);
        Assert.Equal(850000, (int)second.Score);
        Assert.Equal(PhoenixPlate.TalentedGame, second.Plate);
    }

    [Fact]
    public async Task GetAccountDataReportsTheLoginPageAsInvalidRequiringLogin()
    {
        // Wrong-password shape: the site still deposits a session cookie but serves its
        // login page. INVALID + RequiresLogin=true is what keeps OfficialSiteClient mapping
        // this to InvalidCredentialException (the E2E invalid-login flow pins the same shape).
        var html = await File.ReadAllTextAsync(Path.Combine(FixtureRoot, "GetAccountData_LoginPage.html"));
        var api = BuildApi(html);

        var result = await api.GetAccountData(MixEnum.Phoenix, HttpClientReturning(html), CancellationToken.None);

        Assert.Equal("INVALID", (string)result.AccountName);
        Assert.True(result.RequiresLogin);
    }

    [Fact]
    public async Task GetAccountDataReportsAnAuthenticatedProfilelessPageAsInvalidWithoutLogin()
    {
        // Phoenix 2 launch-week state: authenticated fine, but no game profile/card is
        // associated, so my_page renders without the title list AND without a login form.
        // INVALID + RequiresLogin=false lets OfficialSiteClient raise
        // NoGameAccountAssociatedException instead of "wrong password".
        var html = await File.ReadAllTextAsync(Path.Combine(FixtureRoot, "GetAccountData_NoProfile.html"));
        var api = BuildApi(html);

        var result = await api.GetAccountData(MixEnum.Phoenix2, HttpClientReturning(html), CancellationToken.None);

        Assert.Equal("INVALID", (string)result.AccountName);
        Assert.False(result.RequiresLogin);
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("pt-BR")]
    [InlineData("fr-FR")]
    [InlineData("it-IT")]
    public async Task GetPumbilityRankingsParsesEntriesAcrossCultures(string cultureName)
    {
        // PUMBILITY values render as "17,418<span>.45</span>" — "," thousands, "." decimals,
        // whatever the request thread's culture says. Same latent bug class as the pt-BR
        // recent-scores incident; pinned invariant from day one.
        var previousCulture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
        try
        {
            var html = await File.ReadAllTextAsync(
                Path.Combine(FixtureRoot, "GetPumbilityRankings_Phoenix2.html"));
            var api = BuildApi(html);

            var result = await api.GetPumbilityRankings(MixEnum.Phoenix2, null, 1, HttpClientReturning(html),
                CancellationToken.None);

            Assert.Equal(2, result.Entries.Length);
            Assert.Equal("BYEOL#3627", result.Entries[0].ProfileName);
            Assert.Equal("BEGINNER", result.Entries[0].Title);
            Assert.Equal(17418.45, result.Entries[0].Pumbility, 2);
            Assert.Equal(
                new Uri(
                    "https://piugame.com/data/avatar_img2/33ecd96b847c0f8433ca999e63ba6c75.png?v=20260701144004"),
                result.Entries[0].AvatarUrl);
            Assert.Equal("JYUNG#5351", result.Entries[1].ProfileName);
            Assert.Equal("[S] ADVANCED LV.3", result.Entries[1].Title);
            Assert.Equal(16032.26, result.Entries[1].Pumbility, 2);
            // The viewer's own "MY RANKING DATA" block reuses the ranking markup — the
            // service account must never leak into results.
            Assert.DoesNotContain(result.Entries, e => e.ProfileName.StartsWith("DRMURLOC"));
            Assert.False(result.IsEnd);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    [Fact]
    public async Task GetPumbilityRankingsParsesThePhoenixBoard()
    {
        // Phoenix publishes a PUMBILITY board too, on the same page and markup — but its
        // values are whole numbers with no decimal span, its avatars ride /data/avatar_img/,
        // and the whole 1000-player board is one un-paginated page. The parser has to read
        // that shape without an authenticated client (Phoenix's rankings stay anonymous).
        var html = await File.ReadAllTextAsync(
            Path.Combine(FixtureRoot, "GetPumbilityRankings_Phoenix.html"));
        var api = BuildApi(html);

        var result = await api.GetPumbilityRankings(MixEnum.Phoenix, null, 1, null, CancellationToken.None);

        Assert.Equal(2, result.Entries.Length);
        Assert.Equal("FEFEMZ#1489", result.Entries[0].ProfileName);
        Assert.Equal("PIU STEPMAKER", result.Entries[0].Title);
        Assert.Equal(102362, result.Entries[0].Pumbility, 2);
        Assert.Equal(
            new Uri(
                "https://phoenix.piugame.com/data/avatar_img/9516a7cc69a1b2b86c6a3541283ca495.png?v=20250923184201"),
            result.Entries[0].AvatarUrl);
        Assert.Equal("FRANKEZA#9606", result.Entries[1].ProfileName);
        Assert.Equal(100240, result.Entries[1].Pumbility, 2);
        Assert.True(result.IsEnd);
    }

    [Fact]
    public async Task GetPumbilityRankingsReportsTheLastPage()
    {
        // Same fixture minus the pagination icons = the board's final page.
        var html = (await File.ReadAllTextAsync(
                Path.Combine(FixtureRoot, "GetPumbilityRankings_Phoenix2.html")))
            .Replace(@"<i class=""xi next""></i>", "")
            .Replace(@"<i class=""xi last""></i>", "");
        var api = BuildApi(html);

        var result = await api.GetPumbilityRankings(MixEnum.Phoenix2, null, 1, HttpClientReturning(html),
            CancellationToken.None);

        Assert.True(result.IsEnd);
        Assert.Equal(2, result.Entries.Length);
    }

    [Theory]
    [InlineData(null, "t=&")]
    [InlineData(ChartType.Single, "t=s&")]
    [InlineData(ChartType.Double, "t=d&")]
    public async Task GetPumbilityRankingsRequestsTheRightTab(ChartType? chartType, string expectedQuery)
    {
        var html = await File.ReadAllTextAsync(Path.Combine(FixtureRoot, "GetPumbilityRankings_Phoenix2.html"));
        var api = BuildApi(html);
        var (client, requests) = CapturingHttpClientReturning(html);

        await api.GetPumbilityRankings(MixEnum.Phoenix2, chartType, 3, client, CancellationToken.None);

        var request = Assert.Single(requests);
        Assert.Contains("/leaderboard/pumbility_ranking.php", request.ToString());
        Assert.Contains(expectedQuery, request.Query);
        Assert.Contains("page=3", request.Query);
    }

    [Fact]
    public async Task GetPumbilityRankingsRejectsTabsTheBoardDoesNotHave()
    {
        var api = BuildApi("<html></html>");

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            api.GetPumbilityRankings(MixEnum.Phoenix2, ChartType.CoOp, 1, HttpClientReturning("<html></html>"),
                CancellationToken.None));
    }

    [Fact]
    public async Task GetSongLeaderboardParsesThePhoenix2AvatarHost()
    {
        // Phoenix 2 serves avatars from /data/avatar_img2/ (Phoenix uses /avatar_img/) —
        // the recurring avatar-import bug is narrowing the regex to one variant, which
        // silently drops EVERY leaderboard row (the avatar Uri throw hits the per-row
        // catch). This fixture pins the Phoenix 2 shape; the HappyPath fixture pins
        // Phoenix. Break either and this suite goes red, not production imports.
        var html = await File.ReadAllTextAsync(
            Path.Combine(FixtureRoot, "GetSongLeaderboard_Phoenix2Host.html"));
        var api = BuildApi(html);

        var result = await api.GetSongLeaderboard(MixEnum.Phoenix2, songId: "any", page: 1,
            CancellationToken.None);

        Assert.Equal(2, result.Results.Length);
        Assert.Equal("SUNNY#5412", result.Results[0].ProfileName);
        Assert.Equal(996996, result.Results[0].Score);
        Assert.Equal(
            new Uri("https://piugame.com/data/avatar_img2/6ed01094850e66d34aa4831f567363d4.png?v=20260701144004"),
            result.Results[0].AvatarUrl);
        Assert.Equal("JOA#8436", result.Results[1].ProfileName);
        Assert.Equal(995196, result.Results[1].Score);
    }

    [Fact]
    public async Task GetAccountDataParsesThePhoenix2AvatarHost()
    {
        // Same avatar_img2 pin for the account-data path: before the regex accepted the
        // Phoenix 2 host, the profile-image Uri constructor threw and the whole PIUGAME
        // login/import flow failed for Phoenix 2 accounts.
        var html = await File.ReadAllTextAsync(
            Path.Combine(FixtureRoot, "GetAccountData_Phoenix2Avatar.html"));
        var api = BuildApi(html);

        var result = await api.GetAccountData(MixEnum.Phoenix2, HttpClientReturning(html), CancellationToken.None);

        Assert.Equal("DRMURLOC", (string)result.AccountName);
        Assert.Equal(
            new Uri("https://piugame.com/data/avatar_img2/33ecd96b847c0f8433ca999e63ba6c75.png?v=20260701144004"),
            result.ImageUrl);
        Assert.Contains(result.TitleEntries, t => t.Name == "BEGINNER" && t.Have && t.ColClass == "col0");
        Assert.Contains(result.TitleEntries, t => t.Name == "EXC FOLLOWER" && !t.Have && t.ColClass == "col1");
    }

    [Fact]
    public async Task GetChartPopularityReportsRawTilesSeparatelyFromParsedEntries()
    {
        // Three tiles: one clean, one with no song-name node, one whose stepball art moved
        // off the official hosts (the 2026-07-03 drift shape). Skipped tiles must not
        // vanish from the raw count — pagination decisions ride RawRowCount.
        var html = await File.ReadAllTextAsync(
            Path.Combine(FixtureRoot, "GetChartPopularity_MixedParseability.html"));
        var api = BuildApi(html);

        var result = await api.GetChartPopularityLeaderboard(MixEnum.Phoenix2, 0,
            DateTimeOffset.UtcNow, CancellationToken.None, HttpClientReturning(html));

        Assert.Equal(3, result.RawRowCount);
        var entry = Assert.Single(result.Entries);
        Assert.Equal("District 1", (string)entry.SongName);
        Assert.Equal(ChartType.Double, entry.ChartType);
        Assert.Equal(26, (int)entry.ChartLevel);
        Assert.Equal(4, entry.Place);
        Assert.Equal("https://phoenix.piugame.com/data/song_img/district1.png?v=1", entry.SongImage);
    }

    [Fact]
    public async Task ATransientConnectionFailureIsRetriedRatherThanFailingTheFetch()
    {
        // 2026-07-26 incident: the Phoenix 2 sweep died at its first stage on a single reset TLS
        // handshake ("The SSL connection could not be established"), costing a full week of
        // boards. The site's edge resets connections under sweep load and its SSO bounce fails
        // the first request of every session by design — one attempt is never a verdict.
        var html = await File.ReadAllTextAsync(Path.Combine(FixtureRoot, "GetSongLeaderboard_HappyPath.html"));
        var api = BuildApi(html);
        var (client, attempts) = FlakyHttpClientReturning(html, failures: 3,
            () => new HttpRequestException("The SSL connection could not be established"));

        var result = await api.GetSongLeaderboard(MixEnum.Phoenix2, songId: "any", page: 1, CancellationToken.None,
            client);

        Assert.Equal(4, attempts.Count);
        Assert.Equal(2, result.Results.Length);
    }

    [Fact]
    public async Task AConnectionThatNeverRecoversGivesUpAfterFourAttempts()
    {
        var api = BuildApi("<html></html>");
        var (client, attempts) = FlakyHttpClientReturning("<html></html>", failures: int.MaxValue,
            () => new HttpRequestException("The SSL connection could not be established"));

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            api.GetSongLeaderboard(MixEnum.Phoenix2, songId: "any", page: 1, CancellationToken.None, client));

        Assert.Equal(4, attempts.Count);
    }

    [Fact]
    public async Task ACancelledSweepIsNotRetried()
    {
        // Cancellation is a decision, not a transient fault — the old bare catch treated it as
        // one and burned the retry budget shutting down.
        var api = BuildApi("<html></html>");
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        var (client, attempts) = FlakyHttpClientReturning("<html></html>", failures: int.MaxValue,
            () => new OperationCanceledException(cancelled.Token));

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            api.GetSongLeaderboard(MixEnum.Phoenix2, songId: "any", page: 1, cancelled.Token, client));

        Assert.Single(attempts);
    }

    [Fact]
    public async Task ARequestTimeoutIsRetriedEvenThoughItArrivesAsACancellation()
    {
        // HttpClient reports its own 100s request timeout as TaskCanceledException with nobody's
        // token cancelled. That is a hung connection — the exact transient the policy exists for —
        // so it must not be mistaken for the caller calling the run off.
        var html = await File.ReadAllTextAsync(Path.Combine(FixtureRoot, "GetSongLeaderboard_HappyPath.html"));
        var api = BuildApi(html);
        var (client, attempts) = FlakyHttpClientReturning(html, failures: 2,
            () => new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout",
                new TimeoutException()));

        var result = await api.GetSongLeaderboard(MixEnum.Phoenix2, songId: "any", page: 1, CancellationToken.None,
            client);

        Assert.Equal(3, attempts.Count);
        Assert.Equal(2, result.Results.Length);
    }

    // ---- play_data.php: the completeness check's census surface ----

    [Fact]
    public async Task GetPlayDataReadsPhoenixClearHeadlineAndExactPlateCounts()
    {
        var html = await File.ReadAllTextAsync(Path.Combine(FixtureRoot, "GetPlayData_Phoenix.html"));
        var api = BuildApi(html);

        var result = await api.GetPlayData(MixEnum.Phoenix, HttpClientReturning(html), bucket: "",
            CancellationToken.None);

        // "Clear 2,776/3,646" — passes and the mix's chart count, stated by the page itself.
        Assert.Equal(2776, result.Passes);
        Assert.Equal(3646, result.CatalogTotal);

        // Phoenix renders no grade tiles at all, and its plate counts are already exact: they sum
        // to the clear count rather than accumulating toward it.
        Assert.Empty(result.GradeCounts);
        Assert.Equal(1027, result.PlateCounts["mg"]);
        Assert.Equal(129, result.PlateCounts["pg"]);
        Assert.Equal(38, result.PlateCounts["rg"]);
        Assert.Equal(result.Passes, result.PlateCounts.Values.Sum());

        // The level filter starts at 10 on Phoenix — sub-10 has no bucket, which is why the
        // census derives it as a residual there.
        Assert.DoesNotContain("9", result.Buckets);
        Assert.Contains("10", result.Buckets);
        Assert.Contains("27over", result.Buckets);
        Assert.Contains("coop", result.Buckets);
    }

    [Fact]
    public async Task GetPlayDataReadsPhoenixPerLevelBucket()
    {
        var html = await File.ReadAllTextAsync(Path.Combine(FixtureRoot, "GetPlayData_PhoenixLevel.html"));
        var api = BuildApi(html);

        var result = await api.GetPlayData(MixEnum.Phoenix, HttpClientReturning(html), bucket: "25",
            CancellationToken.None);

        // "Clear 21/68" on the level-25 page.
        Assert.Equal("25", result.Bucket);
        Assert.Equal(21, result.Passes);
        Assert.Equal(68, result.CatalogTotal);
        Assert.Equal(21, result.PlateCounts.Values.Sum());
    }

    [Fact]
    public async Task GetPlayDataDeCumulatesPhoenix2GradeAndPlateTiles()
    {
        var html = await File.ReadAllTextAsync(Path.Combine(FixtureRoot, "GetPlayData_Phoenix2.html"));
        var api = BuildApi(html);

        var result = await api.GetPlayData(MixEnum.Phoenix2, HttpClientReturning(html), bucket: "",
            CancellationToken.None);

        // Phoenix 2 has no clear headline; its worst grade tile IS the pass count, because the
        // tiles are cumulative ("this grade or better"). Fixture reads 2,9,11,13,…,16.
        Assert.Equal(16, result.Passes);
        Assert.Equal(4476, result.CatalogTotal);

        // De-cumulated: SSS+ 2 then SSS 9−2, SS+ 11−9, SS 13−11, S+ 13−13 …
        Assert.Equal(2, result.GradeCounts["SSS_PLUS"]);
        Assert.Equal(7, result.GradeCounts["SSS"]);
        Assert.Equal(2, result.GradeCounts["SS_PLUS"]);
        Assert.Equal(2, result.GradeCounts["SS"]);
        Assert.Equal(0, result.GradeCounts["S_PLUS"]);
        Assert.Equal(result.Passes, result.GradeCounts.Values.Sum());
        Assert.Equal(result.Passes, result.PlateCounts.Values.Sum());

        // Its level filter reaches level 1, unlike Phoenix's.
        Assert.Contains("1", result.Buckets);
        Assert.Contains("9", result.Buckets);
    }

    [Fact]
    public async Task GetPlayDataDeCumulatesCorrectlyWhenTheSiteOmitsEmptyTopBands()
    {
        // The site drops a tile whose count is zero. Because the counts are cumulative and
        // monotonic, that can only ever happen at the TOP of the run — this level-17 fixture has
        // no SSS+ tile, so SSS must de-cumulate against an implicit zero rather than skipping.
        var html = await File.ReadAllTextAsync(Path.Combine(FixtureRoot, "GetPlayData_Phoenix2Level.html"));
        var api = BuildApi(html);

        var result = await api.GetPlayData(MixEnum.Phoenix2, HttpClientReturning(html), bucket: "17",
            CancellationToken.None);

        Assert.False(result.GradeCounts.ContainsKey("SSS_PLUS"));
        Assert.Equal(3, result.GradeCounts["SSS"]);
        Assert.Equal(1, result.GradeCounts["SS_PLUS"]);
        Assert.Equal(5, result.Passes);
        Assert.Equal(308, result.CatalogTotal);
        Assert.Equal(result.Passes, result.GradeCounts.Values.Sum());
    }

    // ---- pumbility.php: the live official headline, in two grammars ----

    [Fact]
    public async Task GetPumbilityReadsThePhoenixClassicListAndItsTotal()
    {
        var html = await File.ReadAllTextAsync(Path.Combine(FixtureRoot, "GetPumbility_Phoenix.html"));
        var api = BuildApi(html);

        var result = await api.GetPumbility(MixEnum.Phoenix, HttpClientReturning(html), CancellationToken.None);

        Assert.Equal(64466, result.Total);
        var first = result.Entries.First();
        Assert.Equal("Doppelganger", first.SongName);
        Assert.Equal(ChartType.Double, first.ChartType);
        Assert.Equal(26, first.Level);
        Assert.Equal(PhoenixLetterGrade.AA, first.Grade);
        Assert.Equal(1460, first.Value);
        // Phoenix PUMBILITY is plate-blind and its rows carry no plate art.
        Assert.Null(first.Plate);
    }

    [Fact]
    public async Task GetPumbilityReadsThePhoenix2BreakdownCards()
    {
        var html = await File.ReadAllTextAsync(Path.Combine(FixtureRoot, "GetPumbility_Phoenix2.html"));
        var api = BuildApi(html);

        var result = await api.GetPumbility(MixEnum.Phoenix2, HttpClientReturning(html), CancellationToken.None);

        Assert.Equal(4902.05, result.Total);
        var first = result.Entries.First();
        Assert.Equal("Caprice of DJ Otada", first.SongName);
        Assert.Equal(ChartType.Single, first.ChartType);
        Assert.Equal(21, first.Level);
        Assert.Equal(PhoenixLetterGrade.SS, first.Grade);
        Assert.Equal(PhoenixPlate.MarvelousGame, first.Plate);
        Assert.Equal(354.24, first.Value);

        // A title containing the site's own " - " join survives the split: the page renders
        // "Exceed2 Opening - SHORT CUT - - BanYa".
        Assert.Contains(result.Entries, e => e.SongName == "Exceed2 Opening - SHORT CUT -");
        // Zero-valued rows are meaningful — that is how the page prices sub-10 and broken entries.
        Assert.Contains(result.Entries, e => e.Value == 0);
    }

    // ---- user_play_log_detail: naming the charts inside one count tile ----

    [Fact]
    public async Task GetPlayLogNamesTheChartsBehindAPhoenix2GradeCell()
    {
        var html = await File.ReadAllTextAsync(Path.Combine(FixtureRoot, "GetPlayLog_Phoenix2.html"));
        var api = BuildApi(html);

        var result = await api.GetPlayLog(MixEnum.Phoenix2, HttpClientReturning(html), bucket: "17", type: "A",
            isGrade: true, page: 1, CancellationToken.None);

        // The level-17 A-or-better cell holds five charts and fits on one page.
        Assert.Equal(5, result.Entries.Length);
        Assert.Equal(1, result.MaxPage);
        var first = result.Entries.First();
        Assert.Equal("Ugly duck Toccata", first.SongName);
        Assert.Equal(ChartType.Single, first.ChartType);
        Assert.Equal(17, first.Level);
    }

    [Fact]
    public async Task GetPlayLogReadsThePagerDepthOfALargePhoenixPlateCell()
    {
        var html = await File.ReadAllTextAsync(Path.Combine(FixtureRoot, "GetPlayLog_Phoenix.html"));
        var api = BuildApi(html);

        var result = await api.GetPlayLog(MixEnum.Phoenix, HttpClientReturning(html), bucket: "", type: "mg",
            isGrade: false, page: 1, CancellationToken.None);

        // Six rows a page — half of my_best_score.php — over 1,027 charts is 172 pages, and the
        // pager states the last one even though it only renders a window of buttons. This is the
        // number that decides which enumeration the repair picks.
        Assert.Equal(6, result.Entries.Length);
        Assert.Equal(172, result.MaxPage);
    }

    [Fact]
    public async Task GetPlayLogPicksTheGradeEndpointOnlyForGradeCells()
    {
        var html = await File.ReadAllTextAsync(Path.Combine(FixtureRoot, "GetPlayLog_Phoenix2.html"));
        var (client, requests) = CapturingHttpClientReturning(html);
        var api = BuildApi(html);

        await api.GetPlayLog(MixEnum.Phoenix2, client, "17", "A", isGrade: true, 1, CancellationToken.None);
        await api.GetPlayLog(MixEnum.Phoenix2, client, "17", "mg", isGrade: false, 2, CancellationToken.None);

        // The site serves grade cells from detail2 and plate cells from detail; sending a grade
        // type to the plate endpoint returns an empty modal rather than an error.
        Assert.Contains("user_play_log_detail2.php", requests[0].ToString());
        Assert.Contains("lv=17&type=A&page=1", requests[0].ToString());
        Assert.DoesNotContain("detail2", requests[1].ToString());
        Assert.Contains("page=2", requests[1].ToString());
    }

    private static PiuGameApi BuildApi(string responseHtml)
    {
        return new PiuGameApi(
            HttpClientReturning(responseHtml),
            NullLogger<PiuGameApi>.Instance,
            Mock.Of<ICurrentUserAccessor>(),
            // Zero backoff so the retry tests don't spend the policy's real 1s/2s/4s waits.
            Options.Create(new PiuGameConfiguration { RetryBaseDelayMilliseconds = 0 }));
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

    /// <summary>
    ///     A client whose first <paramref name="failures" /> attempts throw before any response is
    ///     produced — the shape of a handshake that never completed.
    /// </summary>
    private static (HttpClient Client, List<Uri> Attempts) FlakyHttpClientReturning(string html, int failures,
        Func<Exception> fault)
    {
        var attempts = new List<Uri>();
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns<HttpRequestMessage, CancellationToken>((req, _) =>
            {
                attempts.Add(req.RequestUri!);
                if (attempts.Count <= failures) throw fault();

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(html, Encoding.UTF8, "text/html")
                });
            });
        return (new HttpClient(handler.Object), attempts);
    }

    private static (HttpClient Client, List<Uri> Requests) CapturingHttpClientReturning(string html)
    {
        var requests = new List<Uri>();
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => requests.Add(req.RequestUri!))
            .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(html, Encoding.UTF8, "text/html")
            });
        return (new HttpClient(handler.Object), requests);
    }
}
