using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Bunit;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ScoreTracker.CommunityTools.Contracts;
using ScoreTracker.CommunityTools.Contracts.Commands;
using ScoreTracker.CommunityTools.Contracts.Queries;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Pages.Admin;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The review queue. Approving is one click; rejecting is not, because a rejection without a
///     reason produces a resubmission identical to the first.
/// </summary>
public sealed class CommunityToolsReviewPageTests : ComponentTestBase
{
    private static readonly DateTimeOffset Now = new(2026, 8, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid ToolId = Guid.Parse("cccccccc-3333-3333-3333-333333333333");

    private readonly Mock<IMediator> _mediator = new();

    public CommunityToolsReviewPageTests()
    {
        Services.AddSingleton(_mediator.Object);
    }

    /// <summary>User.IsAdmin is derived from the id, so an admin is made by being that id.</summary>
    private static readonly Guid AdminId = Guid.Parse("E38954C4-B1B1-418A-93F6-C4B25C98B713");

    private void GivenAdmin(bool isAdmin)
    {
        CurrentUser.SetupGet(c => c.IsLoggedIn).Returns(true);
        CurrentUser.SetupGet(c => c.User).Returns(new User(
            isAdmin ? AdminId : Guid.NewGuid(), Name.From("DrMurloc"), true, null,
            new Uri("https://piu.test/a.png"), null));
    }

    private void GivenQueue(params ToolRecord[] tools)
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetToolsAwaitingReviewQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(tools);
    }

    private static ToolRecord Pending(WebhookMode mode = WebhookMode.ScorePush,
        string? rejection = null)
    {
        return new ToolRecord(ToolId, Guid.NewGuid(), "TUSA", "Planner", "Plans your sessions.",
            "https://planner.example/", ToolVisibility.PendingApproval, false, mode,
            "https://planner.example/hook", Array.Empty<MixEnum>(), 3, Now, null, rejection, Now,
            "X-Planner-Token", true, true);
    }

    private IRenderedFragment Render()
    {
        return Render(builder =>
        {
            builder.OpenComponent<MudBlazor.MudDialogProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<CommunityToolsReview>(1);
            builder.CloseComponent();
        });
    }

    [Fact]
    public void ANonAdminSeesNothingAndTheQueueIsNeverQueried()
    {
        GivenAdmin(false);
        GivenQueue(Pending());

        var cut = Render();

        Assert.Contains("Admins only", cut.Markup);
        Assert.DoesNotContain("Planner", cut.Markup);
        _mediator.Verify(m => m.Send(It.IsAny<GetToolsAwaitingReviewQuery>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>The three fields approval is actually about, plus who is asking.</summary>
    [Fact]
    public void TheQueueShowsWhatPlayersWillSeeAndWhoRegisteredIt()
    {
        GivenAdmin(true);
        GivenQueue(Pending());

        var cut = Render();

        Assert.Contains("Planner", cut.Markup);
        Assert.Contains("Plans your sessions.", cut.Markup);
        Assert.Contains("https://planner.example/", cut.Markup);
        Assert.Contains("TUSA", cut.Markup);
    }

    [Fact]
    public void AnEmptyQueueSaysSoRatherThanRenderingNothing()
    {
        GivenAdmin(true);
        GivenQueue();

        var cut = Render();

        Assert.Contains("Nothing waiting.", cut.Markup);
    }

    [Fact]
    public void ApproveSendsTheCommand()
    {
        GivenAdmin(true);
        GivenQueue(Pending());
        var cut = Render();

        cut.FindAll("button").First(b => b.TextContent.Contains("Approve")).Click();

        _mediator.Verify(m => m.Send(It.Is<ApproveToolCommand>(c => c.ToolId == ToolId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    ///     Opening the dialog must not be the rejection. A maker's only signal about what to change
    ///     is the reason, so an empty one cannot be sent.
    /// </summary>
    [Fact]
    public void RejectAsksForAReasonBeforeSendingAnything()
    {
        GivenAdmin(true);
        GivenQueue(Pending());
        var cut = Render();

        cut.FindAll("button").First(b => b.TextContent.Contains("Reject")).Click();

        Assert.Contains("What should they change?", cut.Markup);
        _mediator.Verify(m => m.Send(It.IsAny<RejectToolCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void RejectSendsTheReasonTheAdminTyped()
    {
        GivenAdmin(true);
        GivenQueue(Pending());
        var cut = Render();
        cut.FindAll("button").First(b => b.TextContent.Contains("Reject")).Click();

        // Immediate="true" binds on input, not change — a Change() here leaves the field empty and
        // the confirm button disabled, which reads as "the click did nothing".
        cut.Find("textarea").Input("The description doesn't say what it does.");
        // Scoped to the dialog: the card behind it has its own Reject button with the same label.
        cut.Find(".mud-dialog").QuerySelectorAll("button")
            .First(b => b.TextContent.Contains("Reject")).Click();

        _mediator.Verify(m => m.Send(It.Is<RejectToolCommand>(c =>
                c.ToolId == ToolId && c.Reason == "The description doesn't say what it does."),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    ///     A public session-mode tool is offered to everyone, and this is the one screen where that
    ///     decision gets made.
    /// </summary>
    [Fact]
    public void ASessionModeToolCarriesTheExtraWarning()
    {
        GivenAdmin(true);
        GivenQueue(Pending(WebhookMode.PiuGameSession));

        var cut = Render();

        Assert.Contains("asks players for their PIUGame session", cut.Markup);
    }

    [Fact]
    public void AnOrdinaryToolDoesNotCarryTheSessionWarning()
    {
        GivenAdmin(true);
        GivenQueue(Pending());

        var cut = Render();

        Assert.DoesNotContain("asks players for their PIUGame session", cut.Markup);
    }

    /// <summary>
    ///     A resubmission after a rejection is the common case, and the previous reason is what says
    ///     whether they actually addressed it.
    /// </summary>
    [Fact]
    public void APreviousRejectionReasonIsShownOnResubmission()
    {
        GivenAdmin(true);
        GivenQueue(Pending(rejection: "No description."));

        var cut = Render();

        Assert.Contains("No description.", cut.Markup);
    }
}
