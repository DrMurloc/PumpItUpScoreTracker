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
    public async Task PhoenixRatingBoardsStayAnonymous()
    {
        // The Phoenix mirror never logs in — byte-identical to before the P2 arm existed.
        var piuGame = new Mock<IPiuGameApi>();
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
        var client = BuildClient(piuGame);

        var entries = (await client.GetRatingBoards(MixEnum.Phoenix, CancellationToken.None)).ToArray();

        Assert.Single(entries);
        Assert.Equal("S20", entries[0].BoardName);
        Assert.Equal(12345m, entries[0].Value);
        piuGame.Verify(p => p.GetSessionId(It.IsAny<MixEnum>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    private static OfficialSiteClient BuildClient(Mock<IPiuGameApi> piuGame, string? serviceUsername = null,
        string? servicePassword = null)
    {
        return new OfficialSiteClient(piuGame.Object, Mock.Of<IChartRepository>(),
            NullLogger<OfficialSiteClient>.Instance, Mock.Of<IMediator>(), Mock.Of<ICurrentUserAccessor>(),
            Mock.Of<IScoreReader>(), Mock.Of<IFileUploadClient>(), Mock.Of<IOfficialLeaderboardRepository>(),
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
        // the old date cutoff truncated. Three held pages fit inside the window; page 4's
        // upscore is found.
        var h = new ImportHarness();
        var held = new List<Chart>();
        for (var i = 1; i <= 3; i++)
        {
            var chart = h.GivenChart(new ChartBuilder().WithSongName($"Held{i}").WithNoteCount(100).Build());
            h.GivenStoredBest(chart, 950000);
            h.GivenBestScorePage(i, Card(chart, 950000, T0.AddMinutes(-i)));
            held.Add(chart);
        }

        var upscored = h.GivenChart(new ChartBuilder().WithSongName("Upscored").WithNoteCount(100).Build());
        h.GivenStoredBest(upscored, 900000);
        h.GivenBestScorePage(4, Card(upscored, 990000, T0.AddHours(-99)));
        h.GivenBestScorePage(5); // empty → end of list

        var results = (await h.Client.GetRecordedScores(MixEnum.Phoenix2, ImportUserId, "sid", "card1",
            includeBroken: false, maxPages: null, CancellationToken.None)).ToArray();

        Assert.Contains(results, r => r.Chart.Id == upscored.Id && (int)r.Score == 990000);
        h.Api.Verify(a => a.GetBestScores(MixEnum.Phoenix2, It.IsAny<HttpClient>(), 4, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DatedWalkStopsAfterAWindowOfPagesWithNoNewBest()
    {
        // Every card on these pages is already held at an equal result: after the window of
        // no-work pages the walk stops without reading the whole list. A real upscore sits on
        // page 5, past the window — the walk must never reach it (the accepted look-back
        // limit, matching the classic up-score window).
        var h = new ImportHarness();
        for (var i = 1; i <= 4; i++)
        {
            var chart = h.GivenChart(new ChartBuilder().WithSongName($"Held{i}").WithNoteCount(100).Build());
            h.GivenStoredBest(chart, 950000);
            h.GivenBestScorePage(i, Card(chart, 950000, T0.AddMinutes(-i)));
        }

        var beyond = h.GivenChart(new ChartBuilder().WithSongName("Beyond").WithNoteCount(100).Build());
        h.GivenStoredBest(beyond, 900000);
        h.GivenBestScorePage(5, Card(beyond, 999000, T0.AddHours(-99)));

        var results = (await h.Client.GetRecordedScores(MixEnum.Phoenix2, ImportUserId, "sid", "card1",
            includeBroken: false, maxPages: null, CancellationToken.None)).ToArray();

        Assert.DoesNotContain(results, r => r.Chart.Id == beyond.Id);
        h.Api.Verify(a => a.GetBestScores(MixEnum.Phoenix2, It.IsAny<HttpClient>(), 5, It.IsAny<CancellationToken>()),
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
            includeBroken: false, maxPages: null, CancellationToken.None)).ToArray();

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
        var brokenCard = Card(chart, 0, T0, plate: null, isBroken: true);
        h.GivenBestScorePage(1, brokenCard);
        h.GivenBestScorePage(2, brokenCard); // the clamp: out-of-range pages repeat the last page

        var without = (await h.Client.GetRecordedScores(MixEnum.Phoenix2, ImportUserId, "sid", "card1",
            includeBroken: false, maxPages: null, CancellationToken.None)).ToArray();
        var withBroken = (await h.Client.GetRecordedScores(MixEnum.Phoenix2, ImportUserId, "sid", "card1",
            includeBroken: true, maxPages: null, CancellationToken.None)).ToArray();

        Assert.Empty(without);
        var saved = Assert.Single(withBroken);
        Assert.True(saved.IsBroken);
        Assert.Null(saved.Plate);
        Assert.Equal(0, (int)saved.Score);
        Assert.Equal(T0, saved.RecordedAt);
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
            includeBroken: false, maxPages: null, CancellationToken.None)).ToArray();

        var saved = Assert.Single(results);
        Assert.Equal(new JudgementCounts(1100, 14, 1, 1, 14), saved.Judgements);
        Assert.Equal(T0, saved.RecordedAt);
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
            includeBroken: false, maxPages: 2, CancellationToken.None)).ToArray();

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
        DateTimeOffset? recordedAt, PhoenixPlate? plate = PhoenixPlate.FairGame, bool isBroken = false)
    {
        return new PiuGameGetBestScoresResult.ScoreDto
        {
            SongName = chart.Song.Name,
            ChartType = chart.Type,
            Level = chart.Level,
            Score = score,
            Plate = plate,
            IsBroken = isBroken,
            RecordedAt = recordedAt
        };
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

    private sealed class ImportHarness
    {
        private readonly List<Chart> _charts = new();
        private readonly List<ScoreTracker.Domain.Models.RecordedPhoenixScore> _storedBests = new();

        public Mock<IPiuGameApi> Api { get; } = new();
        public Mock<IChartRepository> Charts { get; } = new();
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
            Client = new OfficialSiteClient(Api.Object, Charts.Object, NullLogger<OfficialSiteClient>.Instance,
                Mock.Of<IMediator>(), Mock.Of<ICurrentUserAccessor>(), scores.Object, Mock.Of<IFileUploadClient>(),
                Mock.Of<IOfficialLeaderboardRepository>(), Mock.Of<IBus>(), FakeDateTime.At(T0).Object,
                Mock.Of<IDailyStepReader>(), Options.Create(new PiuGameConfiguration()));
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
