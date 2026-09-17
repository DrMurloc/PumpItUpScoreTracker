using System;
using System.Threading;
using System.Threading.Tasks;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Moq;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.Web.Components;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The Hardmode switch under the Hardmode tab's number (docs/design/hardmode-leaderboard.md D30).
///     Absence of the setting is off, so switching on writes it and switching off clears it.
/// </summary>
public sealed class HardmodeOptInRowTests : ComponentTestBase
{
    private string? _stored;

    public HardmodeOptInRowTests()
    {
        CurrentUser.SetupGet(c => c.IsLoggedIn).Returns(true);
        UiSettings.Setup(u => u.GetSetting(HardmodeOptIn.SettingKey, It.IsAny<CancellationToken>(), It.IsAny<Guid?>()))
            .ReturnsAsync(() => _stored);
        UiSettings.Setup(u => u.SetSetting(HardmodeOptIn.SettingKey, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback((string _, string value, CancellationToken _) => _stored = value)
            .Returns(Task.CompletedTask);
        UiSettings.Setup(u => u.ClearSetting(HardmodeOptIn.SettingKey, It.IsAny<CancellationToken>()))
            .Callback(() => _stored = null)
            .Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task EveryAccountStartsOffAndOneClickSwitchesItOn()
    {
        var cut = RenderComponent<HardmodeOptInRow>();
        cut.WaitForAssertion(() => cut.Find("[data-testid=hardmode-optin-off]"));

        await cut.Find("[data-testid=hardmode-optin-turn-on]").ClickAsync(new MouseEventArgs());

        cut.WaitForAssertion(() => cut.Find("[data-testid=hardmode-optin-on]"));
        Assert.True(HardmodeOptIn.IsOn(new System.Collections.Generic.Dictionary<string, string>
        {
            [HardmodeOptIn.SettingKey] = _stored ?? string.Empty
        }));
    }

    [Fact]
    public async Task SwitchingOffClearsTheSettingRatherThanWritingOne()
    {
        _stored = "true";
        var cut = RenderComponent<HardmodeOptInRow>();
        cut.WaitForAssertion(() => cut.Find("[data-testid=hardmode-optin-on]"));

        await cut.Find("[data-testid=hardmode-optin-turn-off]").ClickAsync(new MouseEventArgs());

        cut.WaitForAssertion(() => cut.Find("[data-testid=hardmode-optin-off]"));
        UiSettings.Verify(u => u.ClearSetting(HardmodeOptIn.SettingKey, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Null(_stored);
    }

    [Fact]
    public void ASignedOutVisitorHasNoSwitch()
    {
        CurrentUser.SetupGet(c => c.IsLoggedIn).Returns(false);

        var cut = RenderComponent<HardmodeOptInRow>();

        Assert.Empty(cut.FindAll(".hmd-optin"));
        UiSettings.Verify(u => u.GetSetting(It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<Guid?>()),
            Times.Never);
    }
}
