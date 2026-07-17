using System;
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
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.OfficialMirror.Domain;
using ScoreTracker.OfficialMirror.Infrastructure;
using ScoreTracker.OfficialMirror.Infrastructure.Apis.Contracts;
using ScoreTracker.OfficialMirror.Infrastructure.Apis.Dtos;
using ScoreTracker.SharedKernel.Enums;
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
}
