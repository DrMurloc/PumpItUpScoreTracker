using System;
using System.Threading;
using Bunit;
using Bunit.TestDoubles;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ScoreTracker.CommunityTools.Contracts.Queries;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Components.CommunityTools;
using ScoreTracker.Web.Services.Contracts;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The Community Tools rollout notice reports a change the rollout made to a profile that
///     already existed, so it is addressed to accounts that predate it and to nobody else. A new
///     account is recognised by its arrival on <c>/Setup</c>, where the notice retires itself: it
///     is modal, and a scrim there covers the one screen a first-run player has to use.
///     <para>
///         An account that predates <c>/Setup</c> has no completion flag either, so the audience
///         test cannot be "has this account finished setup?" — that question reads every
///         long-standing player as brand new, which is the whole point of the pair of facts below.
///     </para>
///     <para>
///         Rendered markup is NOT the observable here: an inline MudDialog portals through
///         MudDialogProvider, which a component-under-test's tree does not have, so the markup is
///         empty whether the notice fired or not — an assertion on it passes for the wrong
///         reason. What separates the two paths is that a notice which decides it is relevant
///         asks whether the player already opted out; a suppressed one returns before that.
///     </para>
/// </summary>
public sealed class CommunityToolsAnnouncementTests : ComponentTestBase
{
    private const string SeenKey = "CommunityToolsAnnouncementSeen";

    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<IUiSettingsAccessor> _uiSettings = new();

    public CommunityToolsAnnouncementTests()
    {
        CurrentUser.Setup(c => c.IsLoggedIn).Returns(true);
        // Public, so the relevance check runs all the way to the opt-out query.
        CurrentUser.Setup(c => c.User).Returns(new User(Guid.NewGuid(), Name.From("Jordan"), true, null,
            new Uri("https://example.invalid/a.png"), null));
        _mediator.Setup(m => m.Send(It.IsAny<GetShareWithAllToolsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        Services.AddSingleton(_mediator.Object);
        Services.AddSingleton(_uiSettings.Object);
    }

    private void ArriveAt(string url) =>
        Services.GetRequiredService<FakeNavigationManager>().NavigateTo(url);

    private void AssertConsidered(Times times) =>
        _mediator.Verify(m => m.Send(It.IsAny<GetShareWithAllToolsQuery>(), It.IsAny<CancellationToken>()),
            times);

    [Fact]
    public void StaysQuietForANewAccountOnTheSetupPage()
    {
        ArriveAt("/Setup?from=Discord");

        RenderComponent<CommunityToolsAnnouncement>();

        AssertConsidered(Times.Never());
    }

    /// <summary>
    ///     …and retires it there rather than deferring it, so finishing setup does not hand the
    ///     player a rollout notice about a change that was never made to their account.
    /// </summary>
    [Fact]
    public void RetiresTheNoticeForANewAccount()
    {
        ArriveAt("/Setup?from=Discord");

        RenderComponent<CommunityToolsAnnouncement>();

        _uiSettings.Verify(u => u.SetSetting(SeenKey, "true", It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    ///     The audience test, stated directly: an account old enough to have never seen
    ///     <c>/Setup</c> still gets the notice. Its completion flag is deliberately left unstubbed
    ///     — the component must reach this conclusion without consulting it, because every account
    ///     that predates the page is missing that flag exactly as a brand-new one is.
    /// </summary>
    [Fact]
    public void ConsidersTheNoticeForAnAccountThatNeverWalkedSetup()
    {
        RenderComponent<CommunityToolsAnnouncement>();

        AssertConsidered(Times.Once());
        _uiSettings.Verify(u => u.GetSetting(IUiSettingsAccessor.SetupCompletedSettingKey,
            It.IsAny<CancellationToken>(), It.IsAny<Guid?>()), Times.Never);
    }

    [Fact]
    public void StaysQuietForAnAccountThatAlreadySawIt()
    {
        _uiSettings.Setup(u => u.GetSetting(SeenKey, It.IsAny<CancellationToken>(), null))
            .ReturnsAsync("true");

        RenderComponent<CommunityToolsAnnouncement>();

        AssertConsidered(Times.Never());
    }

    [Fact]
    public void StaysQuietForAnAnonymousVisitor()
    {
        CurrentUser.Setup(c => c.IsLoggedIn).Returns(false);

        RenderComponent<CommunityToolsAnnouncement>();

        AssertConsidered(Times.Never());
    }
}
