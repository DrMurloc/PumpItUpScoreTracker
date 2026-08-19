using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using ScoreTracker.OfficialMirror.Wiring;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.Exceptions;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.OfficialMirror.Domain;
using ScoreTracker.OfficialMirror.Infrastructure;
using ScoreTracker.OfficialMirror.Infrastructure.Apis.Contracts;
using ScoreTracker.OfficialMirror.Infrastructure.Apis.Dtos;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.TestData;
using ScoreTracker.Tests.TestHelpers;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

/// <summary>
///     The "INVALID" sentinel split (Phoenix 2 rollout): a wrong password serves the site's
///     login page (RequiresLogin) and must stay InvalidCredentialException, while an
///     authenticated account with no game profile/card associated — everyone's launch-week
///     state on Phoenix 2 — must surface as NoGameAccountAssociatedException instead of
///     telling the user their working password is wrong.
/// </summary>
public sealed class OfficialSiteClientTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 4, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GetAccountIdentityThrowsInvalidCredentialsWhenTheSiteServesItsLoginPage()
    {
        var piuGame = ArrangeSessionWithAccountData(MixEnum.Phoenix, new PiuGameGetAccountDataResult
        {
            AccountName = "INVALID",
            ImageUrl = new Uri("/notset", UriKind.Relative),
            RequiresLogin = true
        });
        var client = BuildClient(piuGame);

        await Assert.ThrowsAsync<InvalidCredentialException>(() =>
            client.GetAccountIdentity(MixEnum.Phoenix, "user", "pass", CancellationToken.None));
    }

    [Fact]
    public async Task GetAccountIdentityThrowsNoGameAccountAssociatedWhenAuthenticatedButProfileless()
    {
        var piuGame = ArrangeSessionWithAccountData(MixEnum.Phoenix2, new PiuGameGetAccountDataResult
        {
            AccountName = "INVALID",
            ImageUrl = new Uri("/notset", UriKind.Relative),
            RequiresLogin = false
        });
        var client = BuildClient(piuGame);

        await Assert.ThrowsAsync<NoGameAccountAssociatedException>(() =>
            client.GetAccountIdentity(MixEnum.Phoenix2, "user", "pass", CancellationToken.None));
    }

    [Fact]
    public async Task GetAccountDataThrowsNoGameAccountAssociatedWhenAuthenticatedButProfileless()
    {
        // The import path's first site call — a launch-week P2 import attempt must not be
        // reported as bad credentials either.
        var piuGame = ArrangeSessionWithAccountData(MixEnum.Phoenix2, new PiuGameGetAccountDataResult
        {
            AccountName = "INVALID",
            ImageUrl = new Uri("/notset", UriKind.Relative),
            RequiresLogin = false
        });
        var client = BuildClient(piuGame);

        await Assert.ThrowsAsync<NoGameAccountAssociatedException>(() =>
            client.GetAccountData(MixEnum.Phoenix2, "sid123", null, CancellationToken.None));
    }

    [Theory]
    [InlineData("https://phoenix.piugame.com/data/avatar_img/9516a7cc69a1b2b86c6a3541283ca495.png?v=20250923184201",
        "https://piuimages.arroweclip.se/avatars/9516a7cc69a1b2b86c6a3541283ca495.png")]
    [InlineData("https://piugame.com/data/avatar_img2/33ecd96b847c0f8433ca999e63ba6c75.png?v=20260701144004",
        "https://piuimages.arroweclip.se/avatars/p2/33ecd96b847c0f8433ca999e63ba6c75.png")]
    public async Task AvatarsMirrorIntoAFolderPerSourceDirectory(string source, string expected)
    {
        // Phoenix 2 serves avatars from /data/avatar_img2/. The mirror regex only knew
        // /avatar_img/, so every P2 avatar missed and the board fell back to the default
        // art. Widening it is only half the fix: the two directories REUSE ids for
        // unrelated pictures, so a shared mirror folder would serve whichever mix imported
        // an id first to both.
        var piuGame = ArrangeSessionWithAccountData(MixEnum.Phoenix2, new PiuGameGetAccountDataResult
        {
            AccountName = "BYEOL#3627",
            ImageUrl = new Uri(source)
        });
        piuGame.Setup(p => p.GetCards(It.IsAny<MixEnum>(), It.IsAny<HttpClient>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<GameCardRecord>());
        var upload = new Mock<IFileUploadClient>();
        var unmirrored = new Uri("https://piuimages.arroweclip.se/avatars/never.png");
        upload.Setup(u => u.DoesFileExist(It.IsAny<string>(), out unmirrored, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        upload.Setup(u => u.CopyFromSource(It.IsAny<Uri>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Uri _, string path, CancellationToken _) =>
                new Uri("https://piuimages.arroweclip.se" + path));
        var client = BuildClient(piuGame, fileUpload: upload.Object);

        var identity = await client.GetAccountIdentity(MixEnum.Phoenix2, "user", "pass", CancellationToken.None);

        Assert.Equal(expected, identity.ProfileImage?.ToString());
        upload.Verify(u => u.CopyFromSource(new Uri(source), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ThePersonalImportMirrorsThePhoenix2Avatar()
    {
        // The import path reads the same mirror. Its failure mode was quieter than the
        // leaderboard's: a null avatar makes the saga skip the ProfileImage write entirely
        // and UpdateUserGameProfile coalesce back to the stored one, so a P2 import simply
        // never refreshed your picture.
        var piuGame = ArrangeSessionWithAccountData(MixEnum.Phoenix2, new PiuGameGetAccountDataResult
        {
            AccountName = "DRMURLOC#7251",
            ImageUrl = new Uri(
                "https://piugame.com/data/avatar_img2/33ecd96b847c0f8433ca999e63ba6c75.png?v=20260701144004"),
            TitleEntries = Array.Empty<PiuGameGetAccountDataResult.TitleEntry>()
        });
        var upload = new Mock<IFileUploadClient>();
        var unmirrored = new Uri("https://piuimages.arroweclip.se/avatars/never.png");
        upload.Setup(u => u.DoesFileExist(It.IsAny<string>(), out unmirrored, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        upload.Setup(u => u.CopyFromSource(It.IsAny<Uri>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Uri _, string path, CancellationToken _) =>
                new Uri("https://piuimages.arroweclip.se" + path));
        var client = BuildClient(piuGame, fileUpload: upload.Object);

        var data = await client.GetAccountData(MixEnum.Phoenix2, "sid123", null, CancellationToken.None);

        Assert.Equal("https://piuimages.arroweclip.se/avatars/p2/33ecd96b847c0f8433ca999e63ba6c75.png",
            data.AvatarUrl?.ToString());
    }

    [Fact]
    public async Task AnUnrecognizableAvatarUrlKeepsWhateverThePlayerHas()
    {
        // A miss must never write the bare directory URL over a good avatar.
        var piuGame = ArrangeSessionWithAccountData(MixEnum.Phoenix2, new PiuGameGetAccountDataResult
        {
            AccountName = "BYEOL#3627",
            ImageUrl = new Uri("https://piugame.com/data/avatar_img2/")
        });
        piuGame.Setup(p => p.GetCards(It.IsAny<MixEnum>(), It.IsAny<HttpClient>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<GameCardRecord>());
        var upload = new Mock<IFileUploadClient>();
        var client = BuildClient(piuGame, fileUpload: upload.Object);

        var identity = await client.GetAccountIdentity(MixEnum.Phoenix2, "user", "pass", CancellationToken.None);

        Assert.Null(identity.ProfileImage);
        upload.Verify(u => u.CopyFromSource(It.IsAny<Uri>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static Mock<IPiuGameApi> ArrangeSessionWithAccountData(MixEnum mix,
        PiuGameGetAccountDataResult accountData)
    {
        var piuGame = new Mock<IPiuGameApi>();
        piuGame.Setup(p => p.GetSessionId(mix, "user", "pass", It.IsAny<CancellationToken>()))
            .ReturnsAsync((new HttpClient(), "sid123"));
        piuGame.Setup(p => p.ClientForSid(mix, It.IsAny<string>())).Returns(new HttpClient());
        piuGame.Setup(p => p.GetAccountData(mix, It.IsAny<HttpClient>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(accountData);
        return piuGame;
    }

    [Fact]
    public async Task Phoenix2RatingBoardsComeFromTheThreePumbilityTabsWithCentsIntact()
    {
        // The P2 site's daily PUMBILITY board (All/Single/Double tabs) IS its rating
        // board set — one service login, three boards, decimal values preserved.
        var piuGame = new Mock<IPiuGameApi>();
        piuGame.Setup(p => p.GetSessionId(MixEnum.Phoenix2, "svc", "hunter2", It.IsAny<CancellationToken>()))
            .ReturnsAsync((new HttpClient(), "sid123"));
        piuGame.Setup(p => p.GetPumbilityRankings(MixEnum.Phoenix2, It.IsAny<ChartType?>(), 1,
                It.IsAny<HttpClient>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MixEnum _, ChartType? tab, int _, HttpClient _, CancellationToken _) =>
                new PiuGameGetPumbilityRankingResult
                {
                    IsEnd = true,
                    Entries = new[]
                    {
                        new PiuGameGetPumbilityRankingResult.Entry
                            { ProfileName = $"BYEOL#3627{tab}", Pumbility = 17418.45 },
                        new PiuGameGetPumbilityRankingResult.Entry
                            { ProfileName = $"JYUNG#5351{tab}", Pumbility = 16032.26 }
                    }
                });
        var client = BuildClient(piuGame, serviceUsername: "svc", servicePassword: "hunter2");

        var entries = (await client.GetRatingBoards(MixEnum.Phoenix2, CancellationToken.None)).ToArray();

        Assert.Equal(6, entries.Length);
        Assert.Equal(new[] { "PUMBILITY", "PUMBILITY Singles", "PUMBILITY Doubles" },
            entries.Select(e => e.BoardName).Distinct().ToArray());
        Assert.Equal(17418.45m, entries.First(e => e.BoardName == "PUMBILITY").Value);
        piuGame.Verify(p => p.GetSessionId(MixEnum.Phoenix2, "svc", "hunter2", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Phoenix2RatingBoardsWithoutServiceCredentialsFailLoudly()
    {
        // The P2 boards serve no anonymous traffic — a misconfigured import must say
        // exactly which settings are missing, not silently mirror nothing.
        var piuGame = new Mock<IPiuGameApi>();
        var client = BuildClient(piuGame);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.GetRatingBoards(MixEnum.Phoenix2, CancellationToken.None));

        Assert.Contains("PiuGame:ServiceUsername", exception.Message);
        piuGame.Verify(p => p.GetSessionId(It.IsAny<MixEnum>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Phoenix2PopularityRidesTheServiceSession()
    {
        // top_steps.php is login-gated on Phoenix 2 like every other ranking page — an
        // anonymous POST gets the error page, which parses as zero entries and silently
        // skips the popularity stage.
        var piuGame = new Mock<IPiuGameApi>();
        var session = new HttpClient();
        piuGame.Setup(p => p.GetSessionId(MixEnum.Phoenix2, "svc", "hunter2", It.IsAny<CancellationToken>()))
            .ReturnsAsync((session, "sid123"));
        piuGame.Setup(p => p.GetChartPopularityLeaderboard(MixEnum.Phoenix2, It.IsAny<int>(),
                It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>(), It.IsAny<HttpClient?>()))
            .ReturnsAsync(new PiuGameGetChartPopularityLeaderboardResult
            {
                Entries = Array.Empty<PiuGameGetChartPopularityLeaderboardResult.Entry>()
            });
        var client = BuildClient(piuGame, serviceUsername: "svc", servicePassword: "hunter2");

        await client.GetOfficialChartLeaderboardEntries(MixEnum.Phoenix2, CancellationToken.None);

        piuGame.Verify(p => p.GetChartPopularityLeaderboard(MixEnum.Phoenix2, It.IsAny<int>(),
            It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>(), session), Times.AtLeastOnce);
    }

    [Fact]
    public async Task PhoenixPopularityStaysAnonymous()
    {
        var piuGame = new Mock<IPiuGameApi>();
        piuGame.Setup(p => p.GetChartPopularityLeaderboard(MixEnum.Phoenix, It.IsAny<int>(),
                It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>(), It.IsAny<HttpClient?>()))
            .ReturnsAsync(new PiuGameGetChartPopularityLeaderboardResult
            {
                Entries = Array.Empty<PiuGameGetChartPopularityLeaderboardResult.Entry>()
            });
        var client = BuildClient(piuGame);

        await client.GetOfficialChartLeaderboardEntries(MixEnum.Phoenix, CancellationToken.None);

        piuGame.Verify(p => p.GetChartPopularityLeaderboard(MixEnum.Phoenix, It.IsAny<int>(),
            It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>(), null), Times.AtLeastOnce);
        piuGame.Verify(p => p.GetSessionId(It.IsAny<MixEnum>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PopularityWalkContinuesPastUnparseableTilesAndStopsOnAShortPage()
    {
        // A full page whose tiles all failed parsing must keep the walk alive — the site
        // said 50, so deeper pages exist. Only a short RAW page ends the ranking.
        var piuGame = new Mock<IPiuGameApi>();
        piuGame.Setup(p => p.GetChartPopularityLeaderboard(MixEnum.Phoenix, 0,
                It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>(), It.IsAny<HttpClient?>()))
            .ReturnsAsync(new PiuGameGetChartPopularityLeaderboardResult
            {
                Entries = Array.Empty<PiuGameGetChartPopularityLeaderboardResult.Entry>(),
                RawRowCount = 50
            });
        piuGame.Setup(p => p.GetChartPopularityLeaderboard(MixEnum.Phoenix, 50,
                It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>(), It.IsAny<HttpClient?>()))
            .ReturnsAsync(new PiuGameGetChartPopularityLeaderboardResult
            {
                Entries = Array.Empty<PiuGameGetChartPopularityLeaderboardResult.Entry>(),
                RawRowCount = 30
            });
        var client = BuildClient(piuGame);

        await client.GetOfficialChartLeaderboardEntries(MixEnum.Phoenix, CancellationToken.None);

        piuGame.Verify(p => p.GetChartPopularityLeaderboard(MixEnum.Phoenix, 50,
            It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>(), It.IsAny<HttpClient?>()), Times.Once);
        piuGame.Verify(p => p.GetChartPopularityLeaderboard(MixEnum.Phoenix, It.IsAny<int>(),
            It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>(), It.IsAny<HttpClient?>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task PhoenixRatingBoardsAreThePumbilityBoardPlusThePerLevelLists()
    {
        // Phoenix publishes a PUMBILITY board of its own — the mirror takes it alongside the
        // per-level rating lists the mix also still publishes. Neither needs a login.
        var piuGame = PhoenixRatingBoardApi();
        var client = BuildClient(piuGame);

        var entries = (await client.GetRatingBoards(MixEnum.Phoenix, CancellationToken.None)).ToArray();

        Assert.Equal(new[] { "PUMBILITY", "S20" }, entries.Select(e => e.BoardName).ToArray());
        Assert.Equal(102362m, entries[0].Value);
        Assert.Equal(12345m, entries[1].Value);
        piuGame.Verify(p => p.GetSessionId(It.IsAny<MixEnum>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PhoenixAsksForTheAllTabOnly()
    {
        // Phoenix's board ignores the tab parameter and serves the same list for every one,
        // so asking for Singles and Doubles would mirror three copies of one board under
        // names the rankings view reads as real per-type boards.
        var piuGame = PhoenixRatingBoardApi();
        var client = BuildClient(piuGame);

        await client.GetRatingBoards(MixEnum.Phoenix, CancellationToken.None);

        piuGame.Verify(p => p.GetPumbilityRankings(MixEnum.Phoenix, null, It.IsAny<int>(),
            It.IsAny<HttpClient?>(), It.IsAny<CancellationToken>()), Times.Once);
        piuGame.Verify(p => p.GetPumbilityRankings(MixEnum.Phoenix, It.IsNotNull<ChartType?>(), It.IsAny<int>(),
            It.IsAny<HttpClient?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static Mock<IPiuGameApi> PhoenixRatingBoardApi()
    {
        var piuGame = new Mock<IPiuGameApi>();
        piuGame.Setup(p => p.GetPumbilityRankings(MixEnum.Phoenix, It.IsAny<ChartType?>(), It.IsAny<int>(),
                It.IsAny<HttpClient?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PiuGameGetPumbilityRankingResult
            {
                IsEnd = true,
                Entries = new[]
                {
                    new PiuGameGetPumbilityRankingResult.Entry { ProfileName = "FEFEMZ#1489", Pumbility = 102362 }
                }
            });
        piuGame.Setup(p => p.GetLeaderboards(MixEnum.Phoenix, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PiuGameGetLeaderboardListResult
            {
                Entries = new[] { new PiuGameGetLeaderboardListResult.Entry { Id = "S20", Name = "S20" } }
            });
        piuGame.Setup(p => p.GetLeaderboard(MixEnum.Phoenix, "S20", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PiuGameGetLeaderboardResult
            {
                Entries = new[]
                    { new PiuGameGetLeaderboardResult.Entry { ProfileName = "BYEOL#3627", Rating = 12345 } }
            });
        return piuGame;
    }

    private static OfficialSiteClient BuildClient(Mock<IPiuGameApi> piuGame, string? serviceUsername = null,
        string? servicePassword = null, IFileUploadClient? fileUpload = null)
    {
        return new OfficialSiteClient(piuGame.Object, Mock.Of<IChartRepository>(),
            NullLogger<OfficialSiteClient>.Instance, Mock.Of<IMediator>(), Mock.Of<ICurrentUserAccessor>(),
            Mock.Of<IScoreReader>(), fileUpload ?? Mock.Of<IFileUploadClient>(),
            Mock.Of<IBus>(), FakeDateTime.At(Now).Object, Mock.Of<IDailyStepReader>(),
            Options.Create(new PiuGameConfiguration
            {
                ServiceUsername = serviceUsername,
                ServicePassword = servicePassword
            }));
    }

    // ───────────────────────────────────────────────────────────────────────────
    // The import walk over the best-scores pages: the dated (redesigned) list stops
    // on the up-score window — a run of pages holding nothing new-or-improved — or on
    // page repetition, never on the card's displayed (first-play) date, and recent
    // plays attribute their judgement breakdowns onto the bests they produced.

    private static readonly Guid ImportUserId = Guid.NewGuid();
    private static readonly DateTimeOffset T0 = new(2026, 7, 17, 23, 0, 0, TimeSpan.FromHours(9));

    [Fact]
    public async Task DatedWalkPagesPastAlreadyHeldChartsToReachABuriedUpscore()
    {
        // The redesign sorts by last-played and dates each card by FIRST play, so a replayed
        // upscore can sit pages behind freshly-replayed charts we already hold at the same
        // result. The walk must page through those no-work cards to reach it — the exact case
        // the old date cutoff truncated. Four held pages fit inside the five-page window; page
        // 5's upscore is found.
        var h = new ImportHarness();
        for (var i = 1; i <= 4; i++)
        {
            var chart = h.GivenChart(new ChartBuilder().WithSongName($"Held{i}").WithNoteCount(100).Build());
            h.GivenStoredBest(chart, 950000);
            h.GivenBestScorePage(i, Card(chart, 950000, T0.AddMinutes(-i)));
        }

        var upscored = h.GivenChart(new ChartBuilder().WithSongName("Upscored").WithNoteCount(100).Build());
        h.GivenStoredBest(upscored, 900000);
        h.GivenBestScorePage(5, Card(upscored, 990000, T0.AddHours(-99)));
        h.GivenBestScorePage(6); // empty → end of list

        var results = (await h.Client.GetRecordedScores(MixEnum.Phoenix2, ImportUserId, "sid", "card1",
            includeBroken: false, maxPages: null, CancellationToken.None)).Bests.ToArray();

        Assert.Contains(results, r => r.Chart.Id == upscored.Id && (int)r.Score == 990000);
        h.Api.Verify(a => a.GetBestScores(MixEnum.Phoenix2, It.IsAny<HttpClient>(), 5, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DatedWalkStopsAfterAWindowOfPagesWithNoNewBest()
    {
        // Every card on these pages is already held at an equal result: after five no-work
        // pages the walk stops without reading the whole list. A real upscore sits on page 6,
        // past the window — the walk must never reach it (the accepted look-back limit,
        // matching the classic "five folders back" up-score window).
        var h = new ImportHarness();
        for (var i = 1; i <= 5; i++)
        {
            var chart = h.GivenChart(new ChartBuilder().WithSongName($"Held{i}").WithNoteCount(100).Build());
            h.GivenStoredBest(chart, 950000);
            h.GivenBestScorePage(i, Card(chart, 950000, T0.AddMinutes(-i)));
        }

        var beyond = h.GivenChart(new ChartBuilder().WithSongName("Beyond").WithNoteCount(100).Build());
        h.GivenStoredBest(beyond, 900000);
        h.GivenBestScorePage(6, Card(beyond, 999000, T0.AddHours(-99)));

        var results = (await h.Client.GetRecordedScores(MixEnum.Phoenix2, ImportUserId, "sid", "card1",
            includeBroken: false, maxPages: null, CancellationToken.None)).Bests.ToArray();

        Assert.DoesNotContain(results, r => r.Chart.Id == beyond.Id);
        h.Api.Verify(a => a.GetBestScores(MixEnum.Phoenix2, It.IsAny<HttpClient>(), 6, It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DatedWalkStopsWhenTheSiteClampsToTheSamePage()
    {
        // Out-of-range page numbers serve the last page again — repetition is the end
        // signal on a first (no-watermark) import.
        var h = new ImportHarness();
        var chart = h.GivenChart(new ChartBuilder().WithSongName("Only").WithNoteCount(100).Build());
        var card = Card(chart, 950000, T0);
        h.GivenBestScorePage(1, card);
        h.GivenBestScorePage(2, card);

        var results = (await h.Client.GetRecordedScores(MixEnum.Phoenix2, ImportUserId, "sid", "card1",
            includeBroken: false, maxPages: null, CancellationToken.None)).Bests.ToArray();

        Assert.Single(results);
        h.Api.Verify(a => a.GetBestScores(MixEnum.Phoenix2, It.IsAny<HttpClient>(), 3, It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task BrokenBestsHonorTheIncludeBrokenOptIn()
    {
        var h = new ImportHarness();
        var chart = h.GivenChart(new ChartBuilder().WithSongName("Chimera").WithType(ChartType.Double)
            .WithLevel(26).WithNoteCount(51).Build());
        var brokenCard = Card(chart, 250000, T0, plate: null, isBroken: true);
        h.GivenBestScorePage(1, brokenCard);
        h.GivenBestScorePage(2, brokenCard); // the clamp: out-of-range pages repeat the last page

        var without = (await h.Client.GetRecordedScores(MixEnum.Phoenix2, ImportUserId, "sid", "card1",
            includeBroken: false, maxPages: null, CancellationToken.None)).Bests.ToArray();
        var withBroken = (await h.Client.GetRecordedScores(MixEnum.Phoenix2, ImportUserId, "sid", "card1",
            includeBroken: true, maxPages: null, CancellationToken.None)).Bests.ToArray();

        Assert.Empty(without);
        var saved = Assert.Single(withBroken);
        Assert.True(saved.IsBroken);
        Assert.Null(saved.Plate);
        Assert.Equal(250000, (int)saved.Score);
        Assert.Equal(T0, saved.RecordedAt);
    }

    [Fact]
    public async Task AZeroScoringBrokenBestIsDroppedEvenWhenBrokenBestsAreOptedIn()
    {
        // Someone started the song and let it fail out. The redesigned best list carries those;
        // we never store one, so the opt-in has nothing to save.
        var h = new ImportHarness();
        var chart = h.GivenChart(new ChartBuilder().WithSongName("Chimera").WithType(ChartType.Double)
            .WithLevel(26).WithNoteCount(51).Build());
        var walkOff = Card(chart, 0, T0, plate: null, isBroken: true);
        h.GivenBestScorePage(1, walkOff);
        h.GivenBestScorePage(2, walkOff);

        var scrape = await h.Client.GetRecordedScores(MixEnum.Phoenix2, ImportUserId, "sid", "card1",
            includeBroken: true, maxPages: null, CancellationToken.None);

        Assert.Empty(scrape.Bests);
        Assert.Empty(scrape.Plays);
    }

    [Fact]
    public async Task RecentPlayMatchingTheSavedBestAttributesItsJudgements()
    {
        var h = new ImportHarness();
        var chart = h.GivenChart(new ChartBuilder().WithSongName("ALiVE").WithType(ChartType.Double)
            .WithLevel(21).WithNoteCount(1130).Build());
        var bestCard = Card(chart, 978147, T0);
        h.GivenBestScorePage(1, bestCard);
        h.GivenBestScorePage(2, bestCard); // the clamp: out-of-range pages repeat the last page
        h.GivenRecentScores(
            Play(chart, 978147, T0, perfects: 1100, greats: 14, goods: 1, bads: 1, misses: 14),
            // An earlier, lower play of the same chart must not win the attribution.
            Play(chart, 960000, T0.AddMinutes(-10), perfects: 1000, greats: 60, goods: 30, bads: 20, misses: 20));

        var results = (await h.Client.GetRecordedScores(MixEnum.Phoenix2, ImportUserId, "sid", "card1",
            includeBroken: false, maxPages: null, CancellationToken.None)).Bests.ToArray();

        var saved = Assert.Single(results);
        Assert.Equal(new JudgementCounts(1100, 14, 1, 1, 14), saved.Judgements);
        Assert.Equal(T0, saved.RecordedAt);
    }

    [Fact]
    public async Task TheProducingPlaysOwnTimeBeatsTheBestCardsStamp()
    {
        // The card's date is stamped when the chart first reaches the list and never moves, so a
        // chart failed days ago and passed last night shows the OLD date beside the new score
        // (measured live, 2026-08-18). The play's own time is what the journal keys on, so the
        // pass has to travel with its own — otherwise it lands on the earlier attempt's row.
        var h = new ImportHarness();
        var chart = h.GivenChart(new ChartBuilder().WithSongName("Rush-More").WithType(ChartType.Double)
            .WithLevel(23).WithNoteCount(1000).Build());
        var firstAttempt = T0.AddDays(-2);
        var card = Card(chart, 955291, firstAttempt);
        h.GivenBestScorePage(1, card);
        h.GivenBestScorePage(2, card);
        h.GivenRecentScores(Play(chart, 955291, T0, perfects: 940, greats: 40, goods: 10, bads: 5, misses: 5));

        var saved = Assert.Single((await h.Client.GetRecordedScores(MixEnum.Phoenix2, ImportUserId, "sid", "card1",
            includeBroken: false, maxPages: null, CancellationToken.None)).Bests);

        Assert.Equal(T0, saved.RecordedAt);
        Assert.Equal(new JudgementCounts(940, 40, 10, 5, 5), saved.Judgements);
    }

    [Fact]
    public async Task ABestTheWindowNoLongerReachesKeepsTheCardsStamp()
    {
        // Nothing better is available: the window has moved on, so the card's date is all there is.
        var h = new ImportHarness();
        var chart = h.GivenChart(new ChartBuilder().WithSongName("Long Ago").WithType(ChartType.Single)
            .WithLevel(18).WithNoteCount(600).Build());
        var cardDate = T0.AddDays(-30);
        var card = Card(chart, 970000, cardDate);
        h.GivenBestScorePage(1, card);
        h.GivenBestScorePage(2, card);

        var saved = Assert.Single((await h.Client.GetRecordedScores(MixEnum.Phoenix2, ImportUserId, "sid", "card1",
            includeBroken: false, maxPages: null, CancellationToken.None)).Bests);

        Assert.Equal(cardDate, saved.RecordedAt);
    }

    [Fact]
    public async Task AHigherBrokenRecentPlayNeverBecomesAPassingRecord()
    {
        // The franken-record: Max(score) with All(broken) for the flag used to save the break's
        // 900k wearing the pass's cleared flag — an attempt nobody played. One play wins, and
        // its score, plate and broken flag travel together.
        var h = new ImportHarness();
        var chart = h.GivenChart(new ChartBuilder().WithSongName("Frankenstein").WithType(ChartType.Single)
            .WithLevel(22).WithNoteCount(1000).Build());
        h.GivenBestScorePage(1);
        h.GivenBestScorePage(2);
        h.GivenRecentScores(
            Broken(chart, 900000, T0.AddMinutes(-10), perfects: 800, greats: 50, goods: 10, bads: 5, misses: 20),
            Play(chart, 850000, T0, perfects: 700, greats: 200, goods: 50, bads: 30, misses: 20));

        var saved = Assert.Single((await h.Client.GetRecordedScores(MixEnum.Phoenix2, ImportUserId, "sid", "card1",
            includeBroken: true, maxPages: null, CancellationToken.None)).Bests);

        Assert.False(saved.IsBroken);
        Assert.Equal(850000, (int)saved.Score);
        Assert.Equal(PhoenixPlate.FairGame, saved.Plate);
    }

    [Fact]
    public async Task ABrokenOnlyChartIsFilledInFromRecentPlaysWithoutAPlate()
    {
        // The chart never reached the best list, so the recent window is the only place it
        // exists — and a break carries no plate, whatever the parser handed us.
        var h = new ImportHarness();
        var chart = h.GivenChart(new ChartBuilder().WithSongName("Never Passed").WithType(ChartType.Double)
            .WithLevel(24).WithNoteCount(800).Build());
        h.GivenBestScorePage(1);
        h.GivenBestScorePage(2);
        h.GivenRecentScores(
            Broken(chart, 410000, T0.AddMinutes(-20), perfects: 300, greats: 20, goods: 5, bads: 2, misses: 9),
            Broken(chart, 620000, T0, perfects: 500, greats: 30, goods: 4, bads: 1, misses: 12));

        var withOptIn = (await h.Client.GetRecordedScores(MixEnum.Phoenix2, ImportUserId, "sid", "card1",
            includeBroken: true, maxPages: null, CancellationToken.None)).Bests.ToArray();
        var without = (await h.Client.GetRecordedScores(MixEnum.Phoenix2, ImportUserId, "sid", "card1",
            includeBroken: false, maxPages: null, CancellationToken.None)).Bests.ToArray();

        Assert.Empty(without);
        var saved = Assert.Single(withOptIn);
        Assert.True(saved.IsBroken);
        Assert.Null(saved.Plate);
        // The deeper break wins the group, and its own judgements ride along.
        Assert.Equal(620000, (int)saved.Score);
        Assert.Equal(new JudgementCounts(500, 30, 4, 1, 12), saved.Judgements);
    }

    [Fact]
    public async Task AStageBreakInTheWindowIsAnObservationAndNeverTheBest()
    {
        // The site said the stage broke. Under the opt-in the chart's best is the finished fail
        // beside it — never the break, whatever number the site might have printed for it — and
        // the break itself is journaled with the judgements the card carried and no score.
        var h = new ImportHarness();
        var chart = h.GivenChart(new ChartBuilder().WithSongName("Arcana Force").WithType(ChartType.Double)
            .WithLevel(20).WithNoteCount(1163).Build());
        h.GivenBestScorePage(1);
        h.GivenBestScorePage(2);
        h.GivenRecentScores(
            Broken(chart, 620000, T0.AddMinutes(-20), perfects: 700, greats: 200, goods: 100, bads: 63, misses: 100),
            StageBreak(chart, T0, perfects: 244, greats: 5, goods: 2, bads: 1, misses: 110));

        var scrape = await h.Client.GetRecordedScores(MixEnum.Phoenix2, ImportUserId, "sid", "card1",
            includeBroken: true, maxPages: null, CancellationToken.None);

        var saved = Assert.Single(scrape.Bests);
        Assert.Equal(620000, (int)saved.Score);
        Assert.True(saved.IsBroken);
        Assert.Equal(2, scrape.Plays.Count);
        var stageBreak = Assert.Single(scrape.Plays, p => p.IsStageBroken);
        Assert.Null(stageBreak.Score);
        Assert.True(stageBreak.IsBroken);
        Assert.Equal(T0, stageBreak.PlayedAt);
        Assert.Equal(new JudgementCounts(244, 5, 2, 1, 110), stageBreak.Judgements);
    }

    [Fact]
    public async Task AWindowOfNothingButStageBreaksSeatsNothingAndAnnouncesNoDailyStep()
    {
        var h = new ImportHarness();
        var chart = h.GivenChart(new ChartBuilder().WithSongName("BLAZOR").WithType(ChartType.Double)
            .WithLevel(18).WithNoteCount(888).Build());
        h.GivenDailyStepChart(chart);
        h.GivenBestScorePage(1);
        h.GivenBestScorePage(2);
        h.GivenRecentScores(
            StageBreak(chart, T0.AddMinutes(-5), perfects: 100, greats: 3, goods: 0, bads: 0, misses: 55),
            StageBreak(chart, T0, perfects: 334, greats: 7, goods: 0, bads: 0, misses: 60));

        var scrape = await h.Client.GetRecordedScores(MixEnum.Phoenix2, ImportUserId, "sid", "card1",
            includeBroken: true, maxPages: null, CancellationToken.None);

        Assert.Empty(scrape.Bests);
        Assert.Equal(2, scrape.Plays.Count);
        Assert.All(scrape.Plays, p => Assert.True(p.IsStageBroken));
        h.Bus.Verify(b => b.Publish(It.IsAny<DailyStepScoreObservedEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
        // Nothing to learn a note count from either: a stage break judged fewer notes than the chart has.
        h.Charts.Verify(c => c.UpdateNoteCount(It.IsAny<MixEnum>(), It.IsAny<Guid>(), It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AStageBrokenBestListCardIsJournaledAtItsDateAndSavedNowhere()
    {
        // The redesigned list keeps a stage break as an unpassed chart's first attempt, printing
        // the running score at the moment it broke — 683,059 here, which reads like a near-pass and
        // is not a chart score. It seats nothing under any opt-in; the play is kept, dated, scoreless.
        var h = new ImportHarness();
        var chart = h.GivenChart(new ChartBuilder().WithSongName("Arcana Force").WithType(ChartType.Double)
            .WithLevel(20).WithNoteCount(1163).Build());
        var card = Card(chart, 683059, T0, plate: null, isStageBroken: true);
        h.GivenBestScorePage(1, card);
        h.GivenBestScorePage(2, card);

        var scrape = await h.Client.GetRecordedScores(MixEnum.Phoenix2, ImportUserId, "sid", "card1",
            includeBroken: true, maxPages: null, CancellationToken.None);

        Assert.Empty(scrape.Bests);
        var play = Assert.Single(scrape.Plays);
        Assert.True(play.IsStageBroken);
        Assert.True(play.IsBroken);
        Assert.Null(play.Score);
        Assert.Null(play.Judgements);
        Assert.Equal(chart.Id, play.ChartId);
        Assert.Equal(T0, play.PlayedAt);
    }

    [Fact]
    public async Task AWalkOffCardIsNeitherSavedNorJournaled()
    {
        var h = new ImportHarness();
        var chart = h.GivenChart(new ChartBuilder().WithSongName("Chimera").WithType(ChartType.Double)
            .WithLevel(26).WithNoteCount(1000).Build());
        var walkOff = Card(chart, 0, T0, plate: null, isStageBroken: true);
        h.GivenBestScorePage(1, walkOff);
        h.GivenBestScorePage(2, walkOff);

        var scrape = await h.Client.GetRecordedScores(MixEnum.Phoenix2, ImportUserId, "sid", "card1",
            includeBroken: true, maxPages: null, CancellationToken.None);

        Assert.Empty(scrape.Bests);
        Assert.Empty(scrape.Plays);
    }

    [Fact]
    public async Task AHigherFinishedFailInTheWindowReplacesABrokenCard()
    {
        // D17: the list freezes an unpassed chart's first attempt, so a better finished fail sits
        // in the window unrecorded. Broken may replace broken through the ordinary policy, and
        // the winner brings its own judgements.
        var h = new ImportHarness();
        var chart = h.GivenChart(new ChartBuilder().WithSongName("Tropicanic").WithType(ChartType.Double)
            .WithLevel(13).WithNoteCount(500).Build());
        var card = Card(chart, 426227, T0.AddMinutes(-3), plate: null, isBroken: true);
        h.GivenBestScorePage(1, card);
        h.GivenBestScorePage(2, card);
        h.GivenRecentScores(
            Broken(chart, 944503, T0, perfects: 440, greats: 30, goods: 10, bads: 5, misses: 15));

        var saved = Assert.Single((await h.Client.GetRecordedScores(MixEnum.Phoenix2, ImportUserId, "sid", "card1",
            includeBroken: true, maxPages: null, CancellationToken.None)).Bests);

        Assert.True(saved.IsBroken);
        Assert.Equal(944503, (int)saved.Score);
        Assert.Null(saved.Plate);
        Assert.Equal(new JudgementCounts(440, 30, 10, 5, 15), saved.Judgements);
        Assert.Equal(T0, saved.RecordedAt);
    }

    [Fact]
    public async Task ALowerFinishedFailInTheWindowLeavesTheBrokenCard()
    {
        var h = new ImportHarness();
        var chart = h.GivenChart(new ChartBuilder().WithSongName("Tropicanic").WithType(ChartType.Double)
            .WithLevel(13).WithNoteCount(500).Build());
        var card = Card(chart, 900000, T0.AddMinutes(-3), plate: null, isBroken: true);
        h.GivenBestScorePage(1, card);
        h.GivenBestScorePage(2, card);
        h.GivenRecentScores(
            Broken(chart, 850000, T0, perfects: 400, greats: 50, goods: 20, bads: 10, misses: 20));

        var saved = Assert.Single((await h.Client.GetRecordedScores(MixEnum.Phoenix2, ImportUserId, "sid", "card1",
            includeBroken: true, maxPages: null, CancellationToken.None)).Bests);

        Assert.Equal(900000, (int)saved.Score);
    }

    [Fact]
    public async Task APassingCardIsNeverReplacedFromTheWindow()
    {
        // The list is the truth for a pass (D3): whatever the window holds, a passing card stays.
        var h = new ImportHarness();
        var chart = h.GivenChart(new ChartBuilder().WithSongName("Steady").WithType(ChartType.Single)
            .WithLevel(18).WithNoteCount(600).Build());
        var card = Card(chart, 900000, T0.AddMinutes(-3));
        h.GivenBestScorePage(1, card);
        h.GivenBestScorePage(2, card);
        h.GivenRecentScores(
            Play(chart, 950000, T0, perfects: 560, greats: 30, goods: 5, bads: 2, misses: 3),
            Broken(chart, 990000, T0.AddMinutes(-1), perfects: 590, greats: 5, goods: 2, bads: 1, misses: 2));

        var saved = Assert.Single((await h.Client.GetRecordedScores(MixEnum.Phoenix2, ImportUserId, "sid", "card1",
            includeBroken: true, maxPages: null, CancellationToken.None)).Bests);

        Assert.False(saved.IsBroken);
        Assert.Equal(900000, (int)saved.Score);
    }

    [Fact]
    public async Task StageBrokenCardsDoNotKeepTheDatedWalkGoing()
    {
        // Five pages of stage breaks on charts we do not hold: each would read as "new" if a stage
        // break were work. It is not, so the walk stops at the window and never reads page 6.
        var h = new ImportHarness();
        for (var i = 1; i <= 5; i++)
        {
            var chart = h.GivenChart(new ChartBuilder().WithSongName($"Break{i}").WithNoteCount(100).Build());
            h.GivenBestScorePage(i, Card(chart, 500000 + i, T0.AddMinutes(-i), plate: null, isStageBroken: true));
        }

        var beyond = h.GivenChart(new ChartBuilder().WithSongName("Beyond").WithNoteCount(100).Build());
        h.GivenBestScorePage(6, Card(beyond, 999000, T0.AddHours(-99)));

        var scrape = await h.Client.GetRecordedScores(MixEnum.Phoenix2, ImportUserId, "sid", "card1",
            includeBroken: true, maxPages: null, CancellationToken.None);

        Assert.Empty(scrape.Bests);
        Assert.Equal(5, scrape.Plays.Count);
        h.Api.Verify(a => a.GetBestScores(MixEnum.Phoenix2, It.IsAny<HttpClient>(), 6, It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task EveryDatedRecentPlayIsReturnedAsAnObservation()
    {
        // Non-best plays are journal history; the record only ever takes the winner.
        var h = new ImportHarness();
        var chart = h.GivenChart(new ChartBuilder().WithSongName("Three Runs").WithType(ChartType.Single)
            .WithLevel(19).WithNoteCount(600).Build());
        h.GivenBestScorePage(1, Card(chart, 970000, T0));
        h.GivenBestScorePage(2, Card(chart, 970000, T0));
        h.GivenRecentScores(
            Play(chart, 910000, T0.AddMinutes(-20), perfects: 500, greats: 60, goods: 20, bads: 10, misses: 10),
            Broken(chart, 400000, T0.AddMinutes(-10), perfects: 250, greats: 20, goods: 5, bads: 2, misses: 8),
            Play(chart, 970000, T0, perfects: 560, greats: 30, goods: 5, bads: 2, misses: 3));

        var scrape = await h.Client.GetRecordedScores(MixEnum.Phoenix2, ImportUserId, "sid", "card1",
            includeBroken: false, maxPages: null, CancellationToken.None);

        Assert.Equal(3, scrape.Plays.Count);
        Assert.All(scrape.Plays, p => Assert.Equal(chart.Id, p.ChartId));
        Assert.Equal(new[] { 400000, 910000, 970000 },
            scrape.Plays.Select(p => (int)p.Score).OrderBy(x => x).ToArray());
        // The record still comes off the best list, untouched by the losing plays.
        var saved = Assert.Single(scrape.Bests);
        Assert.Equal(970000, (int)saved.Score);
    }

    [Fact]
    public async Task ClassicWalkReadsLimitPagesThenHuntsUpscoresReusingTheFirstFetch()
    {
        var h = new ImportHarness();
        var chartA = h.GivenChart(new ChartBuilder().WithSongName("Classic A").WithNoteCount(100).Build());
        var chartB = h.GivenChart(new ChartBuilder().WithSongName("Classic B").WithNoteCount(100).Build());
        h.GivenBestScorePage(1, maxPage: 4, Card(chartA, 950000, recordedAt: null));
        h.GivenBestScorePage(2, maxPage: 4, Card(chartB, 940000, recordedAt: null));
        h.GivenBestScorePage(3, maxPage: 4);
        h.GivenBestScorePage(4, maxPage: 4);

        var results = (await h.Client.GetRecordedScores(MixEnum.Phoenix, ImportUserId, "sid", "card1",
            includeBroken: false, maxPages: 2, CancellationToken.None)).Bests.ToArray();

        Assert.Equal(2, results.Length);
        Assert.All(results, r => Assert.Null(r.RecordedAt));
        // Page 1 is fetched exactly once — the pre-walk shape read is reused by the walk.
        h.Api.Verify(a => a.GetBestScores(MixEnum.Phoenix, It.IsAny<HttpClient>(), 1, It.IsAny<CancellationToken>()),
            Times.Once);
        // The up-score hunt continues past the limit to the final page.
        h.Api.Verify(a => a.GetBestScores(MixEnum.Phoenix, It.IsAny<HttpClient>(), 4, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static PiuGameGetBestScoresResult.ScoreDto Card(Chart chart, int score,
        DateTimeOffset? recordedAt, PhoenixPlate? plate = PhoenixPlate.FairGame, bool isBroken = false,
        bool isStageBroken = false)
    {
        return new PiuGameGetBestScoresResult.ScoreDto
        {
            SongName = chart.Song.Name,
            ChartType = chart.Type,
            Level = chart.Level,
            Score = score,
            Plate = plate,
            IsBroken = isBroken || isStageBroken,
            IsStageBroken = isStageBroken,
            RecordedAt = recordedAt
        };
    }

    [Fact]
    public async Task ANoteCountIsOnlyLearnedFromAPassingPlay()
    {
        // The catalog learns a note count once and never revisits it (D13), and only a pass is
        // taken as the sample — a break's breakdown is not asked, whatever it sums to.
        var h = new ImportHarness();
        var chart = h.GivenChart(new ChartBuilder().WithSongName("Unknown Notes").Build());
        h.GivenBestScorePage(1, Card(chart, 950000, T0));
        h.GivenBestScorePage(2, Card(chart, 950000, T0));
        h.GivenRecentScores(
            Broken(chart, 300000, T0.AddMinutes(-10), perfects: 300, greats: 10, goods: 0, bads: 0, misses: 5),
            Play(chart, 950000, T0, perfects: 900, greats: 80, goods: 10, bads: 5, misses: 5));

        await h.Client.GetRecordedScores(MixEnum.Phoenix2, ImportUserId, "sid", "card1",
            includeBroken: false, maxPages: null, CancellationToken.None);

        h.Charts.Verify(c => c.UpdateNoteCount(MixEnum.Phoenix2, chart.Id, 1000,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NoPassingPlayMeansTheNoteCountIsLeftForALaterImport()
    {
        var h = new ImportHarness();
        var chart = h.GivenChart(new ChartBuilder().WithSongName("Only Broken").Build());
        h.GivenBestScorePage(1, Card(chart, 300000, T0, plate: null, isBroken: true));
        h.GivenBestScorePage(2, Card(chart, 300000, T0, plate: null, isBroken: true));
        h.GivenRecentScores(
            Broken(chart, 300000, T0, perfects: 300, greats: 10, goods: 0, bads: 0, misses: 5));

        await h.Client.GetRecordedScores(MixEnum.Phoenix2, ImportUserId, "sid", "card1",
            includeBroken: true, maxPages: null, CancellationToken.None);

        h.Charts.Verify(c => c.UpdateNoteCount(It.IsAny<MixEnum>(), It.IsAny<Guid>(), It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    private static PiuGameGetRecentScoresResult Play(Chart chart, int score,
        DateTimeOffset? recordedAt, int perfects, int greats, int goods, int bads, int misses)
    {
        return new PiuGameGetRecentScoresResult
        {
            SongName = chart.Song.Name,
            ChartType = chart.Type,
            Level = chart.Level,
            Score = score,
            Plate = PhoenixPlate.FairGame,
            NoteCount = perfects + greats + goods + bads + misses,
            IsBroken = false,
            Perfects = perfects,
            Greats = greats,
            Goods = goods,
            Bads = bads,
            Misses = misses,
            RecordedAt = recordedAt
        };
    }

    /// <summary>A failed-but-finished recent play: no plate, an x_ grade, a real score.</summary>
    private static PiuGameGetRecentScoresResult Broken(Chart chart, int score,
        DateTimeOffset? recordedAt, int perfects, int greats, int goods, int bads, int misses)
    {
        var play = Play(chart, score, recordedAt, perfects, greats, goods, bads, misses);
        play.IsBroken = true;
        play.Plate = null;
        return play;
    }

    /// <summary>
    ///     A STAGE BREAK card: no plate, no grade, no score — the song stopped — and judgements
    ///     that stop where it did.
    /// </summary>
    private static PiuGameGetRecentScoresResult StageBreak(Chart chart, DateTimeOffset? recordedAt,
        int perfects, int greats, int goods, int bads, int misses)
    {
        var play = Play(chart, 0, recordedAt, perfects, greats, goods, bads, misses);
        play.IsBroken = true;
        play.IsStageBroken = true;
        play.Score = null;
        play.Plate = null;
        play.Grade = null;
        return play;
    }

    private sealed class ImportHarness
    {
        private readonly List<Chart> _charts = new();
        private readonly List<ScoreTracker.Domain.Models.RecordedPhoenixScore> _storedBests = new();

        public Mock<IPiuGameApi> Api { get; } = new();
        public Mock<IChartRepository> Charts { get; } = new();
        public Mock<IBus> Bus { get; } = new();
        public Mock<IDailyStepReader> DailyStep { get; } = new();
        public HttpClient Session { get; } = new();
        public OfficialSiteClient Client { get; }

        public ImportHarness()
        {
            Api.Setup(a => a.ClientForSid(It.IsAny<MixEnum>(), It.IsAny<string>())).Returns(Session);
            Api.Setup(a => a.GetCards(It.IsAny<MixEnum>(), Session, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { new GameCardRecord("TAG", "card1", true) });
            Api.Setup(a => a.GetAccountData(It.IsAny<MixEnum>(), Session, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PiuGameGetAccountDataResult
                {
                    AccountName = "TAG",
                    ImageUrl = new Uri("https://example.invalid/avatar.png")
                });
            Api.Setup(a => a.GetRecentScores(It.IsAny<MixEnum>(), Session, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<PiuGameGetRecentScoresResult>());
            Charts.Setup(c => c.GetEnglishLookup("ko-KR", It.IsAny<CancellationToken>()))
                .ReturnsAsync((IDictionary<Name, Name>)new Dictionary<Name, Name>());
            Charts.Setup(c => c.GetChartsForSong(It.IsAny<MixEnum>(), It.IsAny<Name>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((MixEnum _, Name name, CancellationToken _) =>
                    _charts.Where(c => c.Song.Name == name).ToArray());
            var scores = new Mock<IScoreReader>();
            scores.Setup(s => s.GetBestScores(It.IsAny<MixEnum>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => _storedBests.ToArray());
            DailyStep.Setup(d => d.GetCurrentChartIds(It.IsAny<MixEnum>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<Guid>());
            Client = new OfficialSiteClient(Api.Object, Charts.Object, NullLogger<OfficialSiteClient>.Instance,
                Mock.Of<IMediator>(), Mock.Of<ICurrentUserAccessor>(), scores.Object, Mock.Of<IFileUploadClient>(),
                Bus.Object, FakeDateTime.At(T0).Object,
                DailyStep.Object, Options.Create(new PiuGameConfiguration()));
        }

        public void GivenDailyStepChart(Chart chart)
        {
            DailyStep.Setup(d => d.GetCurrentChartIds(It.IsAny<MixEnum>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { chart.Id });
        }

        public Chart GivenChart(Chart chart)
        {
            _charts.Add(chart);
            return chart;
        }

        /// <summary>Seeds a stored best the dated walk reads to decide whether a card is work.</summary>
        public Chart GivenStoredBest(Chart chart, int score, PhoenixPlate plate = PhoenixPlate.FairGame,
            bool isBroken = false)
        {
            _storedBests.Add(new ScoreTracker.Domain.Models.RecordedPhoenixScore(chart.Id, score, plate, isBroken, T0));
            return chart;
        }

        public void GivenBestScorePage(int page, params PiuGameGetBestScoresResult.ScoreDto[] cards)
        {
            GivenBestScorePage(page, maxPage: 1, cards);
        }

        public void GivenBestScorePage(int page, int maxPage, params PiuGameGetBestScoresResult.ScoreDto[] cards)
        {
            Api.Setup(a => a.GetBestScores(It.IsAny<MixEnum>(), Session, page, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PiuGameGetBestScoresResult { MaxPage = maxPage, Scores = cards });
        }

        public void GivenRecentScores(params PiuGameGetRecentScoresResult[] plays)
        {
            Api.Setup(a => a.GetRecentScores(It.IsAny<MixEnum>(), Session, It.IsAny<CancellationToken>()))
                .ReturnsAsync(plays);
        }
    }
}
