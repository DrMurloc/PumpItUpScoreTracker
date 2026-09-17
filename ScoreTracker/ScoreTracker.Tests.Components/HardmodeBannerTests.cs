using System;
using System.Threading;
using System.Threading.Tasks;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Moq;
using ScoreTracker.Domain.Models;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.PlayerProgress.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Web.Components;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The one-time Hardmode advert at the top of the Sessions page
///     (docs/design/hardmode-leaderboard.md D31): the owner's page, Phoenix 2, Hardmode off, not
///     dismissed — and nothing otherwise.
/// </summary>
public sealed class HardmodeBannerTests : ComponentTestBase
{
    private static readonly Guid Owner = Guid.NewGuid();
    private string? _dismissed;
    private MixEnum _mix = MixEnum.Phoenix2;
    private string? _optIn;

    public HardmodeBannerTests()
    {
        CurrentUser.SetupGet(c => c.IsLoggedIn).Returns(true);
        CurrentUser.SetupGet(c => c.User).Returns(new User(Owner, "DrMurloc", true, null, null, null));
        UiSettings.Setup(u => u.GetSelectedMix(It.IsAny<CancellationToken>())).ReturnsAsync(() => _mix);
        UiSettings.Setup(u => u.GetSetting(HardmodeBanner.DismissedKey, It.IsAny<CancellationToken>(),
            It.IsAny<Guid?>())).ReturnsAsync(() => _dismissed);
        UiSettings.Setup(u => u.GetSetting(HardmodeOptIn.SettingKey, It.IsAny<CancellationToken>(),
            It.IsAny<Guid?>())).ReturnsAsync(() => _optIn);
        UiSettings.Setup(u => u.SetSetting(HardmodeBanner.DismissedKey, It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback((string _, string value, CancellationToken _) => _dismissed = value)
            .Returns(Task.CompletedTask);
        GivenStanding(new HardmodeStandingRecord(11131.72, 34, 26, 235, 810));
    }

    private void GivenStanding(HardmodeStandingRecord standing)
    {
        Mediator.Setup(m => m.Send(It.Is<GetHardmodeStandingQuery>(q => q.ViewerId == Owner
                                                                         && q.Mix == MixEnum.Phoenix2),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(standing);
    }

    private IRenderedComponent<HardmodeBanner> Render(Guid? owner = null) =>
        RenderComponent<HardmodeBanner>(p => p.Add(c => c.OwnerId, owner ?? Owner));

    [Fact]
    public void QuotesTheOwnersRealStandingAndLinksToTheHardmodeTab()
    {
        var cut = Render();

        cut.WaitForAssertion(() =>
        {
            var banner = cut.Find("[data-testid=hardmode-banner]");
            Assert.Contains("11,132", banner.TextContent);
            Assert.Contains("34 of 50", banner.TextContent);
            Assert.Contains("#26 of 235", banner.TextContent);
            Assert.Equal("/Pumbility/Hardmode",
                cut.Find("[data-testid=hardmode-banner-link]").GetAttribute("href"));
        });
    }

    [Fact]
    public void AnOwnerWithNoHardmodeNumberGetsTheListAndTheField()
    {
        GivenStanding(new HardmodeStandingRecord(0, 0, null, 235, 810));

        var cut = Render();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("810", cut.Find("[data-testid=hardmode-banner-list]").TextContent);
            Assert.Empty(cut.FindAll("[data-testid=hardmode-banner-standing]"));
        });
    }

    [Fact]
    public void AVisitorNeverSeesIt()
    {
        var cut = Render(owner: Guid.NewGuid());

        Assert.Empty(cut.FindAll("[data-testid=hardmode-banner]"));
        Mediator.Verify(m => m.Send(It.IsAny<GetHardmodeStandingQuery>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void ItIsAPhoenixTwoAdvert()
    {
        _mix = MixEnum.Phoenix;

        var cut = Render();

        cut.WaitForAssertion(() => UiSettings.Verify(u => u.GetSelectedMix(It.IsAny<CancellationToken>()),
            Times.Once));
        Assert.Empty(cut.FindAll("[data-testid=hardmode-banner]"));
    }

    [Fact]
    public void AnOwnerWhoSwitchedHardmodeOnIsNotAdvertisedTo()
    {
        _optIn = "true";

        var cut = Render();

        cut.WaitForAssertion(() => UiSettings.Verify(u => u.GetSetting(HardmodeOptIn.SettingKey,
            It.IsAny<CancellationToken>(), It.IsAny<Guid?>()), Times.Once));
        Assert.Empty(cut.FindAll("[data-testid=hardmode-banner]"));
    }

    [Fact]
    public void StaysGoneOnceDismissed()
    {
        _dismissed = "true";

        var cut = Render();

        cut.WaitForAssertion(() => UiSettings.Verify(u => u.GetSetting(HardmodeBanner.DismissedKey,
            It.IsAny<CancellationToken>(), It.IsAny<Guid?>()), Times.Once));
        Assert.Empty(cut.FindAll("[data-testid=hardmode-banner]"));
    }

    [Fact]
    public async Task TheCloseHidesItForGood()
    {
        var cut = Render();
        cut.WaitForAssertion(() => cut.Find("[data-testid=hardmode-banner-dismiss]"));

        await cut.Find("[data-testid=hardmode-banner-dismiss]").ClickAsync(new MouseEventArgs());

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-testid=hardmode-banner]")));
        UiSettings.Verify(u => u.SetSetting(HardmodeBanner.DismissedKey, "true", It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
