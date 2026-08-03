using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using AngleSharp.Dom;
using Bunit;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
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
///     The Webhooks section, as a whole page.
///     <para>
///         The panel inside it is covered on its own; what only exists here is the wiring between
///         them. The panel saves through commands rather than a page load, so the record on screen
///         is whatever the frame last read — and the frame reads once, on parameters. Miss the
///         callback and every control gated on what was just saved stays gated on what it replaced,
///         with nothing on screen to say why. Which is how it shipped: Verify never lit up after a
///         save, and no suite noticed, because the panel was behaving perfectly on its own.
///     </para>
/// </summary>
public sealed class ConsoleWebhooksPageTests : ComponentTestBase
{
    private static readonly Guid ToolId = Guid.Parse("eeeeeeee-5555-5555-5555-555555555555");
    private static readonly Guid OwnerId = Guid.Parse("ffffffff-6666-6666-6666-666666666666");

    private readonly Mock<IMediator> _mediator = new();

    /// <summary>What the next read of the tool returns. Mutated to model a save landing.</summary>
    private ToolRecord _stored = Tool();

    public ConsoleWebhooksPageTests()
    {
        Services.AddSingleton(_mediator.Object);

        CurrentUser.SetupGet(c => c.IsLoggedIn).Returns(true);
        CurrentUser.SetupGet(c => c.User).Returns(new User(OwnerId, Name.From("TUSA"), true, null,
            new Uri("https://piu.test/a.png"), null));

        _mediator.Setup(m => m.Send(It.IsAny<GetMyToolsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new[] { _stored });
    }

    private static ToolRecord Tool(string? webhookUrl = null, bool hasSecret = true)
    {
        return new ToolRecord(ToolId, OwnerId, "TUSA", "Planner", null, "https://planner.example",
            ToolVisibility.Private, true,
            webhookUrl is null ? WebhookMode.None : WebhookMode.ScorePush, webhookUrl,
            Array.Empty<MixEnum>(), 0, DateTimeOffset.Now, null, null, null,
            "X-PIU-Scores-Token", true, hasSecret,
            null, null, null, null, null, false, ToolKind.Integrated, true, webhookUrl is not null);
    }

    private IRenderedComponent<ConsoleWebhooks> Render()
    {
        return RenderComponent<ConsoleWebhooks>(p => p.Add(x => x.ToolId, ToolId));
    }

    private static IElement Button(IRenderedComponent<ConsoleWebhooks> page, string label)
    {
        return page.FindAll("button").First(b => b.TextContent.Trim() == label);
    }

    [Fact]
    public void VerifyLightsUpOnceTheSavedRecordHasTheUrl()
    {
        var page = Render();

        // Delivering somewhere, typed but not yet saved: nothing to verify against.
        Button(page, "Score Push").Click();
        page.FindAll("input").First().Input("https://planner.example/score");
        Assert.True(Button(page, "Verify").HasAttribute("disabled"));

        // The save landing, from the page's point of view: the next read returns the new record.
        // Nothing else tells the page anything happened.
        _stored = Tool("https://planner.example/score");
        Button(page, "Save").Click();

        Assert.False(Button(page, "Verify").HasAttribute("disabled"));
    }

    /// <summary>
    ///     The same seam from the other side: the panel reports a secret is registered only from the
    ///     record, so a stale record leaves the "save one first" nudge on screen after they did.
    /// </summary>
    [Fact]
    public void TheSaveANullSecretNudgeClearsOnceOneIsStored()
    {
        _stored = Tool("https://planner.example/score", hasSecret: false);
        var page = Render();

        Assert.Contains("Save a verification secret first.", page.Markup);

        _stored = Tool("https://planner.example/score");
        Button(page, "Save").Click();

        Assert.DoesNotContain("Save a verification secret first.", page.Markup);
    }
}
