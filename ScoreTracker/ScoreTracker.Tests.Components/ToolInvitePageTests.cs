using System;
using System.Linq;
using System.Threading;
using Bunit;
using Moq;
using ScoreTracker.CommunityTools.Contracts;
using ScoreTracker.CommunityTools.Contracts.Queries;
using ScoreTracker.Web.Pages.CommunityTools;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The invite landing page: what a player is told about a tool before connecting, and what it
///     offers them depending on whether they are signed in.
/// </summary>
public sealed class ToolInvitePageTests : ComponentTestBase
{
    private static readonly Guid Code = Guid.NewGuid();
    private static readonly Guid ToolId = Guid.NewGuid();

    private void GivenPreview(ToolInvitePreview? preview)
    {
        Mediator.Setup(m => m.Send(It.IsAny<GetToolInvitePreviewQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(preview);
    }

    private static ToolInvitePreview Preview()
    {
        return new ToolInvitePreview(ToolId, "PandaGames", null, null, "PIU69", false, false, 0,
            "https://github.com/example/tool", ToolKind.Integrated);
    }

    /// <summary>Inline MudDialogs render through the provider, so the fragment hosts both.</summary>
    private IRenderedFragment Render()
    {
        return Render(builder =>
        {
            builder.OpenComponent<MudBlazor.MudDialogProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<ToolInvite>(1);
            builder.AddAttribute(2, nameof(ToolInvite.Code), Code);
            builder.CloseComponent();
        });
    }

    [Fact]
    public void SigningInFromTheInviteComesBackToIt()
    {
        CurrentUser.SetupGet(c => c.IsLoggedIn).Returns(false);
        GivenPreview(Preview());

        var cut = Render();

        var signIn = cut.FindAll("a").Single(a => a.TextContent.Contains("Sign in to continue"));
        Assert.Equal($"/Login?returnUrl={Uri.EscapeDataString($"/CommunityTools/Invite/{Code}")}",
            signIn.GetAttribute("href"));
    }
}
