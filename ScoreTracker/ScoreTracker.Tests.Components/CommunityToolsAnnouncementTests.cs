using System;
using System.Threading;
using Bunit;
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
///     The Community Tools rollout notice is modal, so where it is allowed to fire matters as
///     much as who sees it. It must stay quiet for an account that has not finished
///     <c>/Setup</c> — that player's first screen is the setup card, and a scrim over it makes
///     the one thing they have to do unclickable.
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

    private void SetupCompleted(bool completed) =>
        _uiSettings.Setup(u => u.GetSetting(IUiSettingsAccessor.SetupCompletedSettingKey,
                It.IsAny<CancellationToken>(), null))
            .ReturnsAsync(completed ? "true" : null);

    private void AssertConsidered(Times times) =>
        _mediator.Verify(m => m.Send(It.IsAny<GetShareWithAllToolsQuery>(), It.IsAny<CancellationToken>()),
            times);

    [Fact]
    public void StaysQuietWhileAnAccountIsStillInSetup()
    {
        SetupCompleted(false);

        RenderComponent<CommunityToolsAnnouncement>();

        AssertConsidered(Times.Never());
    }

    /// <summary>
    ///     …and is not marked seen while suppressed, so the notice is deferred rather than
    ///     burned: the player meets it on the dashboard, which is where it always fired.
    /// </summary>
    [Fact]
    public void DoesNotBurnTheNoticeItSuppressed()
    {
        SetupCompleted(false);

        RenderComponent<CommunityToolsAnnouncement>();

        _uiSettings.Verify(u => u.SetSetting(SeenKey, It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void ConsidersTheNoticeOnceSetupIsDone()
    {
        SetupCompleted(true);

        RenderComponent<CommunityToolsAnnouncement>();

        AssertConsidered(Times.Once());
    }

    [Fact]
    public void StaysQuietForAnAccountThatAlreadySawIt()
    {
        SetupCompleted(true);
        _uiSettings.Setup(u => u.GetSetting(SeenKey, It.IsAny<CancellationToken>(), null))
            .ReturnsAsync("true");

        RenderComponent<CommunityToolsAnnouncement>();

        AssertConsidered(Times.Never());
    }

    [Fact]
    public void StaysQuietForAnAnonymousVisitor()
    {
        CurrentUser.Setup(c => c.IsLoggedIn).Returns(false);
        SetupCompleted(true);

        RenderComponent<CommunityToolsAnnouncement>();

        AssertConsidered(Times.Never());
    }
}
