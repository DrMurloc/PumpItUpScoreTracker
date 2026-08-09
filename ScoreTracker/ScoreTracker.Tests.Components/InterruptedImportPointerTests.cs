using System;
using System.Threading;
using Bunit;
using MediatR;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MudBlazor;
using ScoreTracker.Domain.Models;
using ScoreTracker.OfficialMirror.Contracts;
using ScoreTracker.OfficialMirror.Contracts.Commands;
using ScoreTracker.OfficialMirror.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Web.Components;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The one-time notice for an import a restart cut short
///     (docs/design/import-restart-recovery.md §7). It has to appear exactly once per interrupted
///     run — a notice that repeats is worse than none, and one that never fires leaves a player
///     with silently missing scores.
/// </summary>
public sealed class InterruptedImportPointerTests : ComponentTestBase
{
    private static readonly DateTimeOffset Started = new(2026, 8, 9, 2, 39, 0, TimeSpan.Zero);
    private static readonly Guid UserId = Guid.NewGuid();

    private readonly Mock<IMediator> _mediator = new();

    public InterruptedImportPointerTests()
    {
        Services.AddSingleton(_mediator.Object);
        CurrentUser.SetupGet(u => u.IsLoggedIn).Returns(true);
        CurrentUser.SetupGet(u => u.User).Returns(new User(UserId, "Murloc", true, null, new Uri("https://piu.test/m.png"), null));
    }

    private void GivenUnacknowledged(ImportAttemptRecord? run)
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetUnacknowledgedInterruptedImportQuery>(),
            It.IsAny<CancellationToken>())).ReturnsAsync(run);
    }

    /// <summary>Inline MudDialogs render through the provider, so the fragment hosts both.</summary>
    private IRenderedFragment RenderPointer()
    {
        return Render(builder =>
        {
            builder.OpenComponent<MudDialogProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<InterruptedImportPointer>(1);
            builder.CloseComponent();
        });
    }

    private static ImportAttemptRecord InterruptedRun()
    {
        return new ImportAttemptRecord(Guid.NewGuid(), MixEnum.Phoenix2, ImportKind.Standard, Started,
            Started.AddMinutes(3), ImportOutcome.Interrupted, Guid.NewGuid(), null);
    }

    [Fact]
    public void ShowsNothingWhenThereIsNoInterruptedRun()
    {
        GivenUnacknowledged(null);

        var pointer = RenderComponent<InterruptedImportPointer>();

        Assert.Empty(pointer.Markup.Trim());
    }

    [Fact]
    public void ShowsNothingToAVisitorWhoIsNotSignedIn()
    {
        CurrentUser.SetupGet(u => u.IsLoggedIn).Returns(false);
        GivenUnacknowledged(InterruptedRun());

        var pointer = RenderComponent<InterruptedImportPointer>();

        Assert.Empty(pointer.Markup.Trim());
        _mediator.Verify(m => m.Send(It.IsAny<GetUnacknowledgedInterruptedImportQuery>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void AnInterruptedRunRaisesTheNoticeAndOffersARetry()
    {
        GivenUnacknowledged(InterruptedRun());

        var pointer = RenderPointer();

        Assert.Contains("Your import didn't finish", pointer.Markup);
        Assert.Contains("Import again", pointer.Markup);
        Assert.Contains("Dismiss", pointer.Markup);
    }

    /// <summary>
    ///     The copy never says "failed". The run was cut short and the scores it saved are real, so
    ///     the word would send people hunting for damage that is not there.
    /// </summary>
    [Fact]
    public void TheNoticeDoesNotCallItAFailure()
    {
        GivenUnacknowledged(InterruptedRun());

        var pointer = RenderPointer();

        Assert.DoesNotContain("failed", pointer.Markup, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Import again NAVIGATES. The PIUGame password lives in the player's browser, so nothing
    ///     server-side can re-run the import for them — a button that looked like it would start
    ///     one would be lying.
    /// </summary>
    [Fact]
    public void ImportAgainLinksToTheImportPageRatherThanStartingARun()
    {
        GivenUnacknowledged(InterruptedRun());

        var pointer = RenderPointer();

        Assert.Contains("/UploadPhoenixScores", pointer.Markup);
    }

    /// <summary>
    ///     Acknowledged as it OPENS, not as it closes: a dismissal the player never made — closing
    ///     the tab, navigating away — must not bring the same notice back next page load.
    /// </summary>
    [Fact]
    public void OpeningTheNoticeAcknowledgesTheRun()
    {
        var run = InterruptedRun();
        GivenUnacknowledged(run);

        RenderComponent<InterruptedImportPointer>();

        _mediator.Verify(m => m.Send(
            It.Is<AcknowledgeImportInterruptionCommand>(c => c.ImportResultId == run.Id),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void NothingIsAcknowledgedWhenThereIsNoNoticeToShow()
    {
        GivenUnacknowledged(null);

        RenderComponent<InterruptedImportPointer>();

        _mediator.Verify(m => m.Send(It.IsAny<AcknowledgeImportInterruptionCommand>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }
}
