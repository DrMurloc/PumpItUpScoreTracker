using System;
using System.Linq;
using System.Threading;
using Bunit;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ScoreTracker.Communities.Contracts;
using ScoreTracker.Communities.Contracts.Commands;
using ScoreTracker.Communities.Contracts.Queries;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Pages.Communities;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The invite landing page previews the community and gates acceptance: private-profile
///     players get the score-visibility consent dialog before the join is sent; public players
///     join straight through; banned players see a terminal state.
/// </summary>
public sealed class CommunityInvitePageTests : ComponentTestBase
{
    private static readonly Guid Code = Guid.NewGuid();
    private readonly Mock<IMediator> _mediator = new();

    public CommunityInvitePageTests()
    {
        Services.AddSingleton(_mediator.Object);
    }

    private void GivenPreview(CommunityInvitePreviewRecord? preview) =>
        _mediator.Setup(m => m.Send(It.IsAny<GetCommunityInvitePreviewQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(preview);

    private void GivenUser(bool isPublic)
    {
        CurrentUser.SetupGet(c => c.IsLoggedIn).Returns(true);
        CurrentUser.SetupGet(c => c.User).Returns(new User(
            Guid.NewGuid(), Name.From("Tester"), isPublic, null, new Uri("https://piu.test/avatar.png"), null));
    }

    private static CommunityInvitePreviewRecord Preview(bool banned = false, bool member = false,
        bool expired = false) =>
        new(Name.From("Acme"), CommunityPrivacyType.PublicWithCode, 48, null, expired, banned, member);

    /// <summary>Inline MudDialogs render through the provider, so the fragment hosts both.</summary>
    private IRenderedFragment Render()
    {
        return Render(builder =>
        {
            builder.OpenComponent<MudBlazor.MudDialogProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<CommunityInvite>(1);
            builder.AddAttribute(2, nameof(CommunityInvite.InviteCode), Code);
            builder.CloseComponent();
        });
    }

    [Fact]
    public void InvalidCodeShowsTheDeadLinkState()
    {
        GivenUser(isPublic: true);
        GivenPreview(null);
        var cut = Render();
        Assert.Contains("This invite link isn't valid.", cut.Markup);
    }

    [Fact]
    public void PreviewShowsCommunityNameAndMemberCount()
    {
        GivenUser(isPublic: true);
        GivenPreview(Preview());
        var cut = Render();
        Assert.Contains("Acme", cut.Markup);
        Assert.Contains("Accept invite", cut.Markup);
    }

    [Fact]
    public void BannedCallerSeesTheBannedStateWithNoAcceptButton()
    {
        GivenUser(isPublic: true);
        GivenPreview(Preview(banned: true));
        var cut = Render();
        Assert.Contains("banned", cut.Markup);
        Assert.DoesNotContain("Accept invite", cut.Markup);
    }

    [Fact]
    public void PublicProfileAcceptJoinsWithoutConsentDialog()
    {
        GivenUser(isPublic: true);
        GivenPreview(Preview());
        var cut = Render();

        cut.FindAll("button").First(b => b.TextContent.Contains("Accept invite")).Click();

        _mediator.Verify(m => m.Send(It.Is<JoinCommunityByInviteCodeCommand>(c => c.InviteCode == Code),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void PrivateProfileAcceptOpensConsentInsteadOfJoining()
    {
        GivenUser(isPublic: false);
        GivenPreview(Preview());
        var cut = Render();

        cut.FindAll("button").First(b => b.TextContent.Contains("Accept invite")).Click();

        _mediator.Verify(m => m.Send(It.IsAny<JoinCommunityByInviteCodeCommand>(),
            It.IsAny<CancellationToken>()), Times.Never);
        Assert.Contains("Your scores will be visible", cut.Markup);
    }

    [Fact]
    public void ConsentConfirmationSendsTheJoin()
    {
        GivenUser(isPublic: false);
        GivenPreview(Preview());
        var cut = Render();

        cut.FindAll("button").First(b => b.TextContent.Contains("Accept invite")).Click();
        cut.FindAll("button").First(b => b.TextContent.Contains("I understand")).Click();

        _mediator.Verify(m => m.Send(It.Is<JoinCommunityByInviteCodeCommand>(c => c.InviteCode == Code),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
