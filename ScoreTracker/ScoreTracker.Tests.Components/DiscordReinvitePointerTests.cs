using System;
using System.Threading;
using System.Threading.Tasks;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ScoreTracker.Web.Components;
using ScoreTracker.Web.Services.Contracts;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The re-invite pointer. What matters is that it is driven by the REAL state rather than by
///     "have you seen this yet" — an admin who never re-adds keeps being told — and that a
///     dismissal never flashes on the way in.
/// </summary>
public sealed class DiscordReinvitePointerTests : ComponentTestBase
{
    private const string Invite = "https://discord.com/oauth2/authorize?client_id=1&permissions=268782592";

    private readonly Mock<IUiSettingsAccessor> _settings = new();

    private static readonly Guid CommunityId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private IRenderedComponent<DiscordReinvitePointer> Render(bool show = true) =>
        RenderComponent<DiscordReinvitePointer>(p => p
            .Add(c => c.Show, show)
            .Add(c => c.InviteUrl, Invite)
            .Add(c => c.CommunityId, CommunityId));

    public DiscordReinvitePointerTests()
    {
        Services.AddSingleton(_settings.Object);
    }

    [Fact]
    public void ItShowsWhileThePermissionIsActuallyMissing()
    {
        var pointer = Render();

        Assert.Contains("can't hand out roles", pointer.Markup);
        // The ampersands are HTML-escaped in the href, so match the part that has none.
        Assert.Contains("oauth2/authorize?client_id=1", pointer.Markup);
        Assert.Contains("permissions=268782592", pointer.Markup);
    }

    /// <summary>
    ///     The whole point of driving it from state: a server that has been re-added stops showing
    ///     it, with nothing stored and nothing for the admin to do.
    /// </summary>
    [Fact]
    public void ItDisappearsOnceTheBotCanManageRoles()
    {
        Assert.Empty(Render(show: false).Markup.Trim());
    }

    [Fact]
    public void ADismissedPointerStaysGoneAndNeverFlashes()
    {
        _settings.Setup(s => s.GetSetting(DiscordReinvitePointer.KeyFor(CommunityId),
            It.IsAny<CancellationToken>(), It.IsAny<Guid?>())).ReturnsAsync("true");

        Assert.Empty(Render().Markup.Trim());
    }

    /// <summary>
    ///     The dismissal is per community, because what it reports is per-server state. An admin
    ///     who fixed one community by hand must still be told about the others — and the people who
    ///     run several are exactly who this is for.
    /// </summary>
    [Fact]
    public void DismissingItForOneCommunityLeavesItShowingForAnother()
    {
        _settings.Setup(s => s.GetSetting(DiscordReinvitePointer.KeyFor(Guid.NewGuid()),
            It.IsAny<CancellationToken>(), It.IsAny<Guid?>())).ReturnsAsync("true");

        Assert.Contains("can't hand out roles", Render().Markup);
    }

    [Fact]
    public async Task DismissingWritesTheSettingAndHidesIt()
    {
        var pointer = Render();

        await pointer.Find("[data-testid=discord-reinvite-dismiss]").ClickAsync(new MouseEventArgs());

        _settings.Verify(s => s.SetSetting(DiscordReinvitePointer.KeyFor(CommunityId), "true",
            It.IsAny<CancellationToken>()), Times.Once);
        Assert.Empty(pointer.Markup.Trim());
    }
}
