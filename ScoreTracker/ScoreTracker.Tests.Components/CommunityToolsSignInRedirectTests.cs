using System;
using System.Threading;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ScoreTracker.CommunityTools.Contracts.Queries;
using ScoreTracker.Web.Components.CommunityTools;
using ScoreTracker.Web.Pages.CommunityTools;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The Community Tools pages are about the visitor's own tools and connections. A visitor who
///     is not signed in is sent to sign in, carrying the page they asked for, and nothing that
///     needs an account is asked for on the way.
/// </summary>
public sealed class CommunityToolsSignInRedirectTests : ComponentTestBase
{
    public CommunityToolsSignInRedirectTests()
    {
        CurrentUser.SetupGet(c => c.IsLoggedIn).Returns(false);
    }

    private string Location => Services.GetRequiredService<NavigationManager>().Uri;

    private static string SignInFor(string path)
    {
        return $"http://localhost/Login?returnUrl={Uri.EscapeDataString(path)}";
    }

    [Fact]
    public void TheDirectorySendsALoggedOutVisitorToSignIn()
    {
        Render(builder =>
        {
            builder.OpenComponent<MudBlazor.MudDialogProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<CommunityToolsDirectory>(1);
            builder.CloseComponent();
        });

        Assert.Equal(SignInFor("/CommunityTools"), Location);
        Mediator.Verify(m => m.Send(It.IsAny<GetMyToolConnectionsQuery>(), It.IsAny<CancellationToken>()),
            Times.Never);
        Mediator.Verify(m => m.Send(It.IsAny<GetMyToolsQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void TheDeveloperPageSendsALoggedOutVisitorToSignIn()
    {
        RenderComponent<Developers>();

        Assert.Equal(SignInFor("/Developers"), Location);
        Mediator.Verify(m => m.Send(It.IsAny<GetMyToolsQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void AToolConsoleSendsALoggedOutVisitorToSignInAndBackToTheSameSection()
    {
        var toolId = Guid.NewGuid();
        Services.GetRequiredService<NavigationManager>().NavigateTo($"/Developers/{toolId}/players");

        RenderComponent<ToolConsoleFrame>(p => p
            .Add(f => f.ToolId, toolId)
            .Add(f => f.Section, "players")
            .Add(f => f.ChildContent, _ => builder => builder.AddContent(0, "section")));

        Assert.Equal(SignInFor($"/Developers/{toolId}/players"), Location);
        Mediator.Verify(m => m.Send(It.IsAny<GetMyToolsQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
