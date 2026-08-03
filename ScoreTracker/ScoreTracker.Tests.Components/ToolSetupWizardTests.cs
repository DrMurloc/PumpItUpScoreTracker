using System;
using System.Linq;
using System.Threading;
using AngleSharp.Dom;
using Bunit;
using MediatR;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ScoreTracker.CommunityTools.Contracts;
using ScoreTracker.CommunityTools.Contracts.Commands;
using ScoreTracker.CommunityTools.Contracts.Queries;
using ScoreTracker.Web.Components.CommunityTools;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The first-tool wizard.
///     <para>
///         The behaviour worth pinning is the pacing: what a maker can skip, what they cannot, when
///         they are allowed to leave, and that each screen commits its own work rather than batching
///         to the end — a maker who closes the tab after the key keeps the key.
///     </para>
/// </summary>
public sealed class ToolSetupWizardTests : ComponentTestBase
{
    private static readonly Guid ToolId = Guid.Parse("aaaaaaaa-1111-1111-1111-111111111111");
    private static readonly Guid KeyId = Guid.Parse("bbbbbbbb-2222-2222-2222-222222222222");
    private static readonly Guid InviteCode = Guid.Parse("cccccccc-3333-3333-3333-333333333333");

    private readonly Mock<IMediator> _mediator = new();

    public ToolSetupWizardTests()
    {
        Services.AddSingleton(_mediator.Object);
        _mediator.Setup(m => m.Send(It.IsAny<CreateToolCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ToolId);
        _mediator.Setup(m => m.Send(It.IsAny<CreateToolApiKeyCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MintedApiKey(KeyId, "piu_scores_live_abc", null));
        _mediator.Setup(m => m.Send(It.IsAny<CreateToolInviteLinkCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(InviteCode);
    }

    private IRenderedComponent<ToolSetupWizard> Render()
    {
        return RenderComponent<ToolSetupWizard>();
    }

    private static IElement PrimaryButton(IRenderedComponent<ToolSetupWizard> page)
    {
        return page.FindAll(".ct-wiz-foot button").Last();
    }

    /// <summary>
    ///     Input, not Change: both boxes are Immediate, so they bind on oninput. A Change here
    ///     silently does nothing and every assertion after it reads the previous screen.
    /// </summary>
    private static void TypeName(IRenderedComponent<ToolSetupWizard> page, string value)
    {
        page.FindAll("input:not([type=radio])").First().Input(value);
    }

    /// <summary>Crumbs, not counts — the owner's words: a step count reads as work to endure.</summary>
    [Fact]
    public void TheStepsAreNamedCrumbsAndThereIsNoStepCount()
    {
        var page = Render();

        var crumbs = page.FindAll(".ct-wiz-crumb").Select(c => c.TextContent.Trim()).ToArray();

        Assert.Equal(4, crumbs.Length);
        Assert.Contains(crumbs, c => c.Contains("Name Tool"));
        Assert.Contains(crumbs, c => c.Contains("API Key"));
        Assert.Contains(crumbs, c => c.Contains("Invite Players"));
        Assert.Contains(crumbs, c => c.Contains("Advanced"));
        Assert.DoesNotContain("of 7", page.Markup);
        Assert.DoesNotContain("Step 1", page.Markup);
    }

    [Fact]
    public void TheFirstScreenCannotAdvanceWithoutAName()
    {
        var page = Render();

        Assert.True(PrimaryButton(page).HasAttribute("disabled"));

        TypeName(page, "Murloc Planner");

        Assert.False(PrimaryButton(page).HasAttribute("disabled"));
    }

    /// <summary>
    ///     Committed per screen rather than at the end. A maker who names a tool and wanders off has
    ///     a tool.
    /// </summary>
    [Fact]
    public void NamingCreatesTheToolImmediately()
    {
        var page = Render();
        TypeName(page, "Murloc Planner");

        PrimaryButton(page).Click();

        _mediator.Verify(m => m.Send(It.Is<CreateToolCommand>(c => c.Name == "Murloc Planner"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    ///     There is nothing to leave with before the key exists, so the exit is not offered. Once it
    ///     does, it is.
    /// </summary>
    [Fact]
    public void TheExitAppearsOnlyOnceTheKeyExists()
    {
        var page = Render();
        Assert.DoesNotContain("Finish later", page.Markup);
        Assert.Contains("You can leave once your key exists.", page.Markup);

        TypeName(page, "Murloc Planner");
        PrimaryButton(page).Click();
        TypeName(page, "Production");
        PrimaryButton(page).Click();

        Assert.Contains("Finish later", page.Markup);
        Assert.DoesNotContain("You can leave once your key exists.", page.Markup);
    }

    [Fact]
    public void TheKeyIsShownOnceAndDroppedWhenLeavingThatScreen()
    {
        var page = Render();
        TypeName(page, "Murloc Planner");
        PrimaryButton(page).Click();
        TypeName(page, "Production");
        PrimaryButton(page).Click();

        Assert.Contains("piu_scores_live_abc", page.Markup);

        PrimaryButton(page).Click();

        Assert.DoesNotContain("piu_scores_live_abc", page.Markup);
    }

    /// <summary>
    ///     Session mode is offered here and enabled, which it is not on the console once players
    ///     connect. A brand-new tool has none, so this is the one moment it is freely choosable —
    ///     and copying the console's Disabled binding across would take that away.
    ///     <para>
    ///         Asserts the option's availability rather than driving a selection: MudRadio registers
    ///         its handler as <c>onclick:stoppropagation</c>, which bUnit's dispatcher will not match
    ///         under either spelling.
    ///     </para>
    /// </summary>
    [Fact]
    public void SessionModeIsOfferedAndSelectableOnTheWebhookScreen()
    {
        var page = AtWebhookScreen();

        Assert.Contains("PIUGame session", page.Markup);

        var radios = page.FindAll("input[type=radio]");
        Assert.Equal(4, radios.Count);
        Assert.All(radios, r => Assert.False(r.HasAttribute("disabled")));
    }

    /// <summary>Nothing is the default, and choosing it saves no webhook at all.</summary>
    [Fact]
    public void ChoosingNothingSavesNoWebhook()
    {
        var page = AtWebhookScreen();

        PrimaryButton(page).Click();

        _mediator.Verify(m => m.Send(It.IsAny<SetToolWebhookCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    ///     The listing screen is skippable, and skipping it must not send a tool with no description
    ///     into a review queue it would fail out of.
    /// </summary>
    [Fact]
    public void SkippingTheListingScreenAsksForNothing()
    {
        var page = AtWebhookScreen();
        PrimaryButton(page).Click();

        Assert.Contains("Ask to be listed", page.Markup);
        PrimaryButton(page).Click();

        _mediator.Verify(m => m.Send(It.IsAny<RequestToolListingCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
        Assert.Contains("You're set up", page.Markup);
    }

    private IRenderedComponent<ToolSetupWizard> AtWebhookScreen()
    {
        var page = Render();
        TypeName(page, "Murloc Planner");
        PrimaryButton(page).Click();
        TypeName(page, "Production");
        PrimaryButton(page).Click();
        // Past the reveal.
        PrimaryButton(page).Click();
        // Past the invite screen, which offers a link rather than making one.
        PrimaryButton(page).Click();
        return page;
    }
}
