using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bunit;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MudBlazor;
using AngleSharp.Dom;
using ScoreTracker.Domain.Models;
using ScoreTracker.Identity.Contracts.Queries;
using ScoreTracker.OfficialMirror.Contracts;
using ScoreTracker.OfficialMirror.Contracts.Commands;
using ScoreTracker.OfficialMirror.Contracts.Events;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Web.Components;
using ScoreTracker.Web.Services.UiNotifications;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The Score check panel: a line and two peer buttons. Anything a run finds is already saved,
///     so there is nothing to approve, nothing to remember between visits, and the deep scan's
///     budget lives on the deep scan button.
/// </summary>
public sealed class ScoreCheckPanelTests : ComponentTestBase
{
    private readonly UiNotificationHub _hub = new();
    private readonly Mock<IMediator> _mediator = new();
    private readonly Guid _me = Guid.NewGuid();

    public ScoreCheckPanelTests()
    {
        CurrentUser.SetupGet(c => c.IsLoggedIn).Returns(true);
        CurrentUser.SetupGet(c => c.User)
            .Returns(new User(_me, "Me", true, null, new Uri("https://piu.test/me.png"), null));
        Services.AddSingleton(_mediator.Object);
        Services.AddSingleton<IUiNotificationHub>(_hub);
        Services.AddSingleton(Mock.Of<ISnackbar>());
        Scans(3);
    }

    private void Scans(int left)
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetDeepScansRemainingQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(left);
    }

    private IRenderedComponent<ScoreCheckPanel> Render(bool credentials = true, bool busy = false)
    {
        return RenderComponent<ScoreCheckPanel>(p => p
            .Add(c => c.Mix, MixEnum.Phoenix)
            .Add(c => c.CardId, "card")
            .Add(c => c.GameTag, "TAG #1")
            .Add(c => c.Busy, busy)
            .Add(c => c.Credentials, () => credentials ? new TypedCredentialSource("user", "pass") : null));
    }

    /// <summary>Delivers a finished run the way the saga does — over the hub, never from storage.</summary>
    private Task Finish(IRenderedComponent<ScoreCheckPanel> panel, int added, int checkedCount = 2851)
    {
        return panel.InvokeAsync(() => _hub.PublishAsync(UiTopics.User(_me),
            new ImportCheckCompletedEvent(_me, MixEnum.Phoenix, added, checkedCount)));
    }

    private static IElement Button(IRenderedComponent<ScoreCheckPanel> panel, string text)
    {
        return panel.FindAll("button").First(b => b.TextContent.Contains(text));
    }

    [Fact]
    public void BothActionsAreOfferedFromTheStartWithTheDeepScanBudgetOnItsOwnButton()
    {
        var markup = Render().Markup;

        Assert.Contains("Import and check", markup);
        Assert.Contains("Deep scan", markup);
        // The limit belongs on the control it limits, not in a sentence that only appears when the
        // census happens to come back clean.
        Assert.Contains("3 left this month", markup);
    }

    [Fact]
    public async Task AFinishedRunReportsWhatItAddedAndPointsAtTheSession()
    {
        var panel = Render();

        await Finish(panel, added: 3);

        Assert.Contains("Added 3 scores", panel.Markup);
        Assert.Contains("sessions page", panel.Markup);
        // Nothing to approve — the scores are already saved.
        Assert.DoesNotContain("Add these", panel.Markup);
    }

    [Fact]
    public async Task ACleanAccountSaysSoAndOffersNoSessionLink()
    {
        var panel = Render();

        await Finish(panel, added: 0);

        Assert.Contains("Nothing missing", panel.Markup);
        Assert.Contains("2,851", panel.Markup);
        Assert.DoesNotContain("sessions page", panel.Markup);
    }

    [Fact]
    public async Task BothButtonsStayAvailableAfterARun()
    {
        var panel = Render();

        await Finish(panel, added: 0);

        // Wanting a deep scan does not depend on what the last check said.
        Assert.Contains("Import and check", panel.Markup);
        Assert.Contains("Deep scan", panel.Markup);
    }

    [Fact]
    public void AnExhaustedAllowanceDisablesTheDeepScanAndNamesTheUnlock()
    {
        Scans(0);

        var panel = Render();

        Assert.True(Button(panel, "Deep scan").HasAttribute("disabled"));
        Assert.False(Button(panel, "Import and check").HasAttribute("disabled"));
        Assert.Contains("None left", panel.Markup);
    }

    [Fact]
    public void TheRunningStateSaysLeavingIsFine()
    {
        _mediator.Setup(m => m.Send(It.IsAny<StartImportCheckCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ImportCheckStartResult(ImportCheckStartOutcome.Started, 3));
        var panel = Render();

        Button(panel, "Import and check").Click();

        // Nothing is held in the page any more, so leaving costs nothing.
        Assert.Contains("You can leave", panel.Markup);
    }

    [Fact]
    public void TheDeepScanButtonAsksForADeepScan()
    {
        var started = new List<StartImportCheckCommand>();
        _mediator.Setup(m => m.Send(It.IsAny<StartImportCheckCommand>(), It.IsAny<CancellationToken>()))
            .Callback((object c, CancellationToken _) => started.Add((StartImportCheckCommand)c))
            .ReturnsAsync(new ImportCheckStartResult(ImportCheckStartOutcome.Started, 2));
        var panel = Render();

        Button(panel, "Deep scan").Click();

        Assert.True(Assert.Single(started).DeepScan);
    }

    [Fact]
    public void EveryActionIsBlockedWithoutACredential()
    {
        var buttons = Render(credentials: false).FindAll("button");

        Assert.NotEmpty(buttons);
        Assert.All(buttons, b => Assert.True(b.HasAttribute("disabled")));
    }

    [Fact]
    public void EveryActionIsBlockedWhileTheImportItselfIsRunning()
    {
        var buttons = Render(busy: true).FindAll("button");

        Assert.NotEmpty(buttons);
        Assert.All(buttons, b => Assert.True(b.HasAttribute("disabled")));
    }
}
