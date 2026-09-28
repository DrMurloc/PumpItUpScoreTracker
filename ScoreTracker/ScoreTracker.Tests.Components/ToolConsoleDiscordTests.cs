using System;
using System.Linq;
using System.Threading;
using Bunit;
using Moq;
using ScoreTracker.CommunityTools.Contracts;
using ScoreTracker.CommunityTools.Contracts.Queries;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Pages.CommunityTools;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     What the console tells a maker about their linked Discord account — the one thing that lets
///     anyone but them connect. The settings card offers the link or shows the account; the invite
///     links say plainly when nobody can use them yet.
/// </summary>
public sealed class ToolConsoleDiscordTests : ComponentTestBase
{
    private static readonly Guid ToolId = Guid.Parse("dddddddd-4444-4444-4444-444444444444");
    private static readonly Guid MakerId = Guid.Parse("ffffffff-6666-6666-6666-666666666666");
    private static readonly Guid AdminId = Guid.Parse("E38954C4-B1B1-418A-93F6-C4B25C98B713");
    private const string DiscordId = "123456789012345678";

    private void SignedInAs(Guid userId)
    {
        CurrentUser.SetupGet(c => c.IsLoggedIn).Returns(true);
        CurrentUser.SetupGet(c => c.User).Returns(new User(userId, Name.From("PIU69"), true, null,
            new Uri("https://piu.test/a.png"), null));
    }

    private void GivenTool(string? discordId, string? handle, bool? canTakePlayers = null)
    {
        var tool = new ToolRecord(ToolId, MakerId, "PIU69", "PandaGames", null, null, ToolVisibility.Private,
            true, WebhookMode.None, null, Array.Empty<MixEnum>(), 0, DateTimeOffset.Now, null, null, null,
            null, false, false, "https://github.com/example/tool", "example", DateTimeOffset.Now,
            canTakePlayers ?? discordId is not null, discordId, handle, ToolKind.Integrated, true, false);
        Mediator.Setup(m => m.Send(It.IsAny<GetMyToolsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { tool });
        Mediator.Setup(m => m.Send(It.IsAny<GetToolQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(tool);
        Mediator.Setup(m => m.Send(It.IsAny<GetToolInviteLinksQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ToolInviteLinkRecord>());
    }

    private IRenderedFragment Settings()
    {
        return Render(builder =>
        {
            builder.OpenComponent<MudBlazor.MudDialogProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<ConsoleTool>(1);
            builder.AddAttribute(2, nameof(ConsoleTool.ToolId), ToolId);
            builder.CloseComponent();
        });
    }

    private IRenderedComponent<ConsolePlayers> Players()
    {
        return RenderComponent<ConsolePlayers>(p => p.Add(x => x.ToolId, ToolId));
    }

    private static string? LinkTo(IRenderedFragment page, string text)
    {
        return page.FindAll("a").Single(a => a.TextContent.Contains(text)).GetAttribute("href");
    }

    [Fact]
    public void AMakerWithNoDiscordIsOfferedTheLinkAndBroughtBackHere()
    {
        SignedInAs(MakerId);
        GivenTool(null, null);

        var page = Settings();

        Assert.Contains("Contact &amp; source", page.Markup);
        Assert.Contains("Not linked", page.Markup);
        Assert.Contains("Link your Discord to let other players share their scores with this tool.", page.Markup);
        Assert.Contains("Only you can use this tool right now.", page.Markup);
        Assert.Equal($"/Login/Discord/Link?returnUrl={Uri.EscapeDataString($"/Developers/{ToolId}")}",
            LinkTo(page, "Link Discord"));
    }

    [Fact]
    public void AMakerWithDiscordLinkedSeesTheAccountDrMurlocWillReach()
    {
        SignedInAs(MakerId);
        GivenTool(DiscordId, "stepmaniac_77");

        var page = Settings();

        Assert.Contains("@stepmaniac_77", page.Markup);
        Assert.Contains("DrMurloc can reach you here if anything goes wrong, for your players or for you.", page.Markup);
        Assert.Equal("/Account?tab=signin", LinkTo(page, "Account page"));
        Assert.DoesNotContain("Link Discord", page.Markup);
        Assert.DoesNotContain("Only you can use this tool right now.", page.Markup);
    }

    // The id is stored and the handle is asked of Discord, so a lookup that fails still leaves a way
    // to the account.
    [Fact]
    public void ALinkedAccountDiscordCouldNotNameStillLinksToItsProfile()
    {
        SignedInAs(MakerId);
        GivenTool(DiscordId, null);

        var page = Settings();

        Assert.Equal($"https://discord.com/users/{DiscordId}", LinkTo(page, "View on Discord"));
    }

    [Fact]
    public void TheSourceLinkIsOptionalAndNothingChecksIt()
    {
        SignedInAs(MakerId);
        GivenTool(DiscordId, "stepmaniac_77");

        var page = Settings();

        Assert.Contains("Optional. Players see a link to it next to your tool.", page.Markup);
        Assert.DoesNotContain("Check the link", page.Markup);
        Assert.DoesNotContain("Your Discord handle", page.Markup);
    }

    // Linking attaches an account to whoever is signed in, so an admin on someone else's console is
    // not offered it.
    [Fact]
    public void AnAdminOnSomeoneElsesToolIsNotOfferedTheLink()
    {
        SignedInAs(AdminId);
        GivenTool(null, null);

        var page = Settings();

        Assert.Contains("Not linked", page.Markup);
        Assert.DoesNotContain("Link Discord", page.Markup);
    }

    [Fact]
    public void InviteLinksSayNobodyCanUseThemUntilDiscordIsLinked()
    {
        SignedInAs(MakerId);
        GivenTool(null, null);

        var page = Players();

        Assert.Contains("Nobody can connect through these links yet.", page.Markup);
        Assert.Equal($"/Login/Discord/Link?returnUrl={Uri.EscapeDataString($"/Developers/{ToolId}/players")}",
            LinkTo(page, "Link Discord"));
    }

    [Fact]
    public void InviteLinksCarryNoWarningOnceDiscordIsLinked()
    {
        SignedInAs(MakerId);
        GivenTool(DiscordId, "stepmaniac_77");

        var page = Players();

        Assert.DoesNotContain("Nobody can connect through these links yet.", page.Markup);
    }
}
