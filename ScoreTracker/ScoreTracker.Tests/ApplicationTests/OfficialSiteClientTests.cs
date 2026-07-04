using System;
using System.Net.Http;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
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
            client.GetAccountData(MixEnum.Phoenix2, "user", "pass", null, CancellationToken.None));
    }

    private static Mock<IPiuGameApi> ArrangeSessionWithAccountData(MixEnum mix,
        PiuGameGetAccountDataResult accountData)
    {
        var piuGame = new Mock<IPiuGameApi>();
        piuGame.Setup(p => p.GetSessionId(mix, "user", "pass", It.IsAny<CancellationToken>()))
            .ReturnsAsync((new HttpClient(), "sid123"));
        piuGame.Setup(p => p.GetAccountData(mix, It.IsAny<HttpClient>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(accountData);
        return piuGame;
    }

    private static OfficialSiteClient BuildClient(Mock<IPiuGameApi> piuGame)
    {
        return new OfficialSiteClient(piuGame.Object, Mock.Of<IChartRepository>(),
            NullLogger<OfficialSiteClient>.Instance, Mock.Of<IMediator>(), Mock.Of<ICurrentUserAccessor>(),
            Mock.Of<IScoreReader>(), Mock.Of<IFileUploadClient>(), Mock.Of<IOfficialLeaderboardRepository>(),
            Mock.Of<IBus>(), FakeDateTime.At(Now).Object);
    }
}
