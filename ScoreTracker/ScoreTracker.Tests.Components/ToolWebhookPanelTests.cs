using System;
using System.Linq;
using System.Threading;
using AngleSharp.Dom;
using Bunit;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ScoreTracker.CommunityTools.Contracts;
using ScoreTracker.CommunityTools.Contracts.Commands;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Web.Components.CommunityTools;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The delivery-configuration panel.
///     <para>
///         What is pinned here is the seam between what the maker typed and what the server was
///         told. Verify runs against the <em>saved</em> URL, so every question the button asks is
///         about the record rather than the box — and the panel does not own the record. It asks its
///         parent to re-read it. A panel that never asks, or a parent that never listens, leaves
///         Verify disabled forever with nothing on screen explaining why, which is exactly how this
///         shipped.
///     </para>
/// </summary>
public sealed class ToolWebhookPanelTests : ComponentTestBase
{
    private static readonly Guid ToolId = Guid.Parse("dddddddd-4444-4444-4444-444444444444");

    private readonly Mock<IMediator> _mediator = new();

    public ToolWebhookPanelTests()
    {
        Services.AddSingleton(_mediator.Object);
    }

    /// <summary>
    ///     A tool mid-setup: delivering, both secrets registered, so the only box on screen is the
    ///     URL and the only question left is whether the endpoint answers.
    /// </summary>
    private static ToolRecord Configured(string? savedUrl = "https://planner.example/score",
        bool hasSecret = true, bool hasHeader = true)
    {
        return new ToolRecord(ToolId, Guid.NewGuid(), "Maker", "Planner", null,
            "https://planner.example", ToolVisibility.Private, true,
            savedUrl is null ? WebhookMode.None : WebhookMode.ScorePush, savedUrl,
            new[] { MixEnum.Phoenix }, 0, DateTimeOffset.Now, null, null, null,
            hasHeader ? "X-PIU-Scores-Token" : null, hasHeader, hasSecret,
            null, null, null, null, null, false, ToolKind.Integrated, true, savedUrl is not null);
    }

    private IRenderedComponent<ToolWebhookPanel> Render(ToolRecord tool,
        Action? onChanged = null)
    {
        return RenderComponent<ToolWebhookPanel>(p =>
        {
            p.Add(x => x.Tool, tool);
            if (onChanged is not null) p.Add(x => x.OnChanged, onChanged);
        });
    }

    private static IElement Button(IRenderedComponent<ToolWebhookPanel> panel, string label)
    {
        return panel.FindAll("button").First(b => b.TextContent.Trim() == label);
    }

    /// <summary>Immediate binding, so oninput — a Change here would leave the field unbound.</summary>
    private static void TypeUrl(IRenderedComponent<ToolWebhookPanel> panel, string value)
    {
        panel.FindAll("input").First().Input(value);
    }

    [Fact]
    public void VerifyIsOfferedForASavedUrl()
    {
        var panel = Render(Configured());

        Assert.False(Button(panel, "Verify").HasAttribute("disabled"));
    }

    /// <summary>
    ///     The whole reason the button is gated: the command carries only the tool id, so verifying
    ///     an edited-but-unsaved box would test the previous endpoint and report a pass for one the
    ///     maker has already moved off.
    /// </summary>
    [Fact]
    public void VerifyIsWithheldWhileTheBoxHoldsAnUnsavedUrl()
    {
        var panel = Render(Configured());

        TypeUrl(panel, "https://planner.example/v2/score");

        Assert.True(Button(panel, "Verify").HasAttribute("disabled"));
        Assert.Contains("Save first.", panel.Markup);
    }

    /// <summary>
    ///     A bare host round-trips through <see cref="Uri" /> and comes back with a trailing slash
    ///     the maker never typed. Compared as text that reads as a pending edit, which disabled
    ///     Verify permanently for anyone whose endpoint is a bare host.
    /// </summary>
    [Fact]
    public void ATrailingSlashFromTheServerIsNotAnUnsavedEdit()
    {
        var panel = Render(Configured("https://planner.example/"));

        TypeUrl(panel, "https://planner.example");

        Assert.False(Button(panel, "Verify").HasAttribute("disabled"));
    }

    [Fact]
    public void VerifyIsWithheldUntilAVerificationSecretExists()
    {
        var panel = Render(Configured(hasSecret: false));

        Assert.True(Button(panel, "Verify").HasAttribute("disabled"));
    }

    /// <summary>
    ///     The panel changes the record through commands rather than a page load, so nothing on
    ///     screen reflects a save unless the parent re-reads it. Everything gated on the saved
    ///     record — Verify above all — stays gated on what it replaced otherwise.
    /// </summary>
    [Fact]
    public void SavingAsksTheParentToRereadTheRecord()
    {
        var reloads = 0;
        var panel = Render(Configured(), () => reloads++);

        Button(panel, "Save").Click();

        Assert.Equal(1, reloads);
    }

    /// <summary>
    ///     The header's name is ours now, so a tool that has never set one still has somewhere for
    ///     the first value to go. While the panel required a stored name, the first save on every
    ///     new tool failed on a rule about a field the maker cannot see.
    /// </summary>
    [Fact]
    public void AFirstHeaderValueSavesWithNoStoredHeaderName()
    {
        var panel = Render(Configured(hasHeader: false));

        panel.FindAll("input[type=password]").First().Change("s3cret");
        Button(panel, "Save").Click();

        _mediator.Verify(m => m.Send(
            It.Is<SetToolOutboundHeaderCommand>(c => c.ToolId == ToolId && c.Value == "s3cret"),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
