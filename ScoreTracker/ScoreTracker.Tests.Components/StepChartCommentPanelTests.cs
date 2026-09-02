using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bunit;
using MediatR;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Moq;
using ScoreTracker.ChartComments.Contracts;
using ScoreTracker.ChartComments.Contracts.Commands;
using ScoreTracker.ChartComments.Contracts.Queries;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Components.ChartComments;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The comment surface of the step chart (docs/design/step-chart-comments D4/D13): the bar
///     under the chips and the sticky panel. The module's side of the seam is exercised on a
///     browser harness; these pin what the component renders for each state the module can put
///     it in — browsing a comment, paging a stack, a note, the composer after a pick, the scope
///     chip, and the signed-out reader — through the JSInvokable entry points the module calls,
///     with no JS at all.
/// </summary>
public sealed class StepChartCommentPanelTests : ComponentTestBase
{
    private static readonly Guid Chart = Guid.Parse("cccccccc-0000-0000-0000-00000000000c");

    private readonly User _viewer = new(Guid.NewGuid(), Name.From("ERRLENA"), true, null,
        new Uri("https://example.com/a.png"), Name.From("US"));

    public StepChartCommentPanelTests()
    {
        CurrentUser.Setup(u => u.IsLoggedIn).Returns(true);
        CurrentUser.SetupGet(u => u.User).Returns(() => _viewer);
        Mediator.Setup(m => m.Send(It.IsAny<GetMyCommentScopesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new CommentScopeRecord(CommentAudience.Public, Name.From("Public")),
                new CommentScopeRecord(CommentAudience.Private, Name.From("Notes"))
            });
        Mediator.Setup(m => m.Send(It.IsAny<GetCommentConsentQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommentConsentRecord(false, false));
        Counts((CommentAudience.Public, 1), (CommentAudience.Private, 1));
        Marks();
    }

    /// <summary>What the filter renders from (D18): how many anchored comments each scope holds.</summary>
    private void Counts(params (CommentAudience Audience, int Count)[] counts)
    {
        Mediator.Setup(m => m.Send(It.IsAny<GetChartCommentScopeCountsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(counts.Select(c => new CommentScopeCountRecord(c.Audience, c.Count)).ToArray());
    }

    private void Marks(params CommentRecord[] marks)
    {
        Mediator.Setup(m => m.Send(It.IsAny<GetChartCommentMarksQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(marks);
    }

    /// <summary>Marks that depend on the scope asked for — Public has some, Notes has none.</summary>
    private void MarksByScope(params CommentRecord[] publicMarks)
    {
        Mediator.Setup(m => m.Send(It.IsAny<GetChartCommentMarksQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GetChartCommentMarksQuery query, CancellationToken _) =>
                query.Audience.IsPrivate ? Array.Empty<CommentRecord>() : publicMarks);
    }

    private static CommentRecord Mark(decimal second, string text, string author = "JUNO", int votes = 6,
        int replies = 0, bool note = false, bool isAuthor = false)
    {
        var replyRecords = Enumerable.Range(0, replies)
            .Select(_ => new CommentRecord(Guid.NewGuid(), Chart, Guid.NewGuid(), Name.From("KAMI"), Name.From("KR"),
                null, new[] { CommentSpan.OfText("agreed") }, 0, false, false, false, DateTimeOffset.UtcNow, null,
                null, Array.Empty<CommentRecord>()))
            .ToArray();

        return new CommentRecord(Guid.NewGuid(), Chart, Guid.NewGuid(), Name.From(author), Name.From("KR"), null,
            new[] { CommentSpan.OfText(text) }, votes, false, isAuthor, false, DateTimeOffset.UtcNow.AddDays(-2),
            null, null, replyRecords, null, second, note);
    }

    private IRenderedComponent<StepChartCommentPanel> Render(
        Action<ComponentParameterCollectionBuilder<StepChartCommentPanel>>? more = null)
    {
        return RenderComponent<StepChartCommentPanel>(p =>
        {
            p.Add(c => c.ChartId, Chart).Add(c => c.Active, true);
            more?.Invoke(p);
        });
    }

    [Fact]
    public void TheBarTeachesTheGestureAndTheEmptyPanelIsOneLine()
    {
        var panel = Render();

        var bar = panel.Find("[data-testid='sc-bar']");
        Assert.Contains("Double-click a spot to leave a comment", bar.TextContent);
        Assert.Contains("Double-tap a spot to leave a comment", bar.TextContent);
        Assert.Equal("Nothing here yet.", panel.Find("[data-testid='sc-empty']").TextContent.Trim());
        // No ＋ anywhere: the gesture is the way in (D13).
        Assert.Empty(panel.FindAll("[data-testid='sc-add']"));
    }

    [Fact]
    public void ASignedOutReaderGetsNoBarAndTheQuietEmptyState()
    {
        CurrentUser.Setup(u => u.IsLoggedIn).Returns(false);

        var panel = Render();

        Assert.Empty(panel.FindAll("[data-testid='sc-bar']"));
        Assert.Equal("Nothing here yet.", panel.Find("[data-testid='sc-empty']").TextContent.Trim());
    }

    [Fact]
    public async Task TheBarChipSwitchesTheScopeEvenWhenTheNewScopeIsEmpty()
    {
        MarksByScope(Mark(29m, "The drills start here.", "ERRLENA"));
        var panel = Render();
        Assert.Equal("0:29", panel.Find("[data-testid='sc-second']").TextContent.Trim());

        await panel.Find("[data-testid='sc-scope-bar']").ClickAsync(new MouseEventArgs());
        await panel.Find("[data-testid='sc-scope-item-Notes']").ClickAsync(new MouseEventArgs());

        // Empty scope, and the way back is still there — the redline that started round 3.
        Assert.Equal("Nothing here yet.", panel.Find("[data-testid='sc-empty']").TextContent.Trim());
        var chip = panel.Find("[data-testid='sc-scope-bar']");
        Assert.Contains("Personal Notes", chip.TextContent);
        Assert.Contains("sc-scope-chip-you", chip.ClassName);

        await chip.ClickAsync(new MouseEventArgs());
        await panel.Find("[data-testid='sc-scope-item-Public']").ClickAsync(new MouseEventArgs());

        Assert.Equal("0:29", panel.Find("[data-testid='sc-second']").TextContent.Trim());
    }

    [Fact]
    public async Task TheFilterListsOnlyTheScopesThatHaveCommentsAndTheComposerListsThemAll()
    {
        Counts((CommentAudience.Public, 1), (CommentAudience.Private, 0));
        Marks(Mark(29m, "The drills start here."));
        var panel = Render();

        await panel.Find("[data-testid='sc-scope-bar']").ClickAsync(new MouseEventArgs());

        // Reading: only the scopes with something on this chart, and nothing beside a name (D18).
        var menu = panel.Find(".sc-bar .sc-scope-menu");
        Assert.Single(menu.QuerySelectorAll("[role='menuitem']"));
        Assert.NotEmpty(panel.FindAll("[data-testid='sc-scope-item-Public']"));
        Assert.Empty(panel.FindAll("[data-testid='sc-scope-item-Notes']"));
        Assert.DoesNotContain("Only you", menu.TextContent);

        // Writing is a different question: the first note has to go somewhere.
        await panel.InvokeAsync(() => panel.Instance.OnPick(33.45m));
        await panel.Find("[data-testid='sc-scope-compose']").ClickAsync(new MouseEventArgs());

        Assert.Equal("Personal Notes", panel.Find("[data-testid='sc-scope-item-Notes']").TextContent.Trim());
    }

    [Fact]
    public void AChartWithNoCommentsAnywhereHasNoFilter()
    {
        Counts((CommentAudience.Public, 0), (CommentAudience.Private, 0));

        var panel = Render();

        Assert.NotEmpty(panel.FindAll("[data-testid='sc-bar']"));
        Assert.Empty(panel.FindAll("[data-testid='sc-scope-bar']"));
    }

    [Fact]
    public void WhenPublicIsQuietThePanelOpensOnTheFirstScopeThatHasSomething()
    {
        Counts((CommentAudience.Public, 0), (CommentAudience.Private, 1));
        Mediator.Setup(m => m.Send(It.IsAny<GetChartCommentMarksQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GetChartCommentMarksQuery query, CancellationToken _) => query.Audience.IsPrivate
                ? new[] { Mark(66.2m, "Breathe before the hold.", note: true, isAuthor: true) }
                : Array.Empty<CommentRecord>());

        var panel = Render();

        Assert.Contains("Personal Notes", panel.Find("[data-testid='sc-scope-bar']").TextContent);
        Assert.Equal("1:06", panel.Find("[data-testid='sc-second']").TextContent.Trim());
    }

    [Fact]
    public void TheFirstMarkShowsUntilTheStripSaysOtherwise()
    {
        Marks(Mark(29m, "The drills start here.", "ERRLENA", 14, 2), Mark(33.45m, "This quad is a bracket."));

        var panel = Render();

        Assert.Equal("0:29", panel.Find("[data-testid='sc-second']").TextContent.Trim());
        Assert.Contains("ERRLENA", panel.Find(".sc-name").TextContent);
        Assert.Contains("The drills start here.", panel.Find("[data-testid='sc-body']").TextContent);
        Assert.Contains("▲ 14", panel.Find("[data-testid='sc-vote']").TextContent);
        Assert.Contains("2 replies", panel.Find("[data-testid='sc-replies']").TextContent);
        Assert.True(panel.Find("[data-testid='sc-prev']").HasAttribute("disabled"));
        Assert.False(panel.Find("[data-testid='sc-next']").HasAttribute("disabled"));
        Assert.Contains("Open in Comments", panel.Find("[data-testid='sc-thread']").TextContent);
        // The browse head carries no scope menu of its own any more (D13).
        Assert.Empty(panel.FindAll(".sc-panel [data-testid^='sc-scope']"));
    }

    [Fact]
    public async Task FollowMovesThePanelAndTheStepperPagesAStack()
    {
        var first = Mark(29m, "The drills start here.", "ERRLENA");
        var quadA = Mark(33.45m, "This quad is a bracket.", "JUNO");
        var quadB = Mark(33.45m, "Same quad, heel-toe.", "SOOJIN");
        Marks(first, quadA, quadB);
        var panel = Render();

        await panel.InvokeAsync(() => panel.Instance.OnFollow(quadA.Id));

        Assert.Equal("0:33", panel.Find("[data-testid='sc-second']").TextContent.Trim());
        Assert.Equal("1/2", panel.Find("[data-testid='sc-pager']").TextContent.Trim());

        await panel.Find("[data-testid='sc-next']").ClickAsync(new MouseEventArgs());

        Assert.Equal("2/2", panel.Find("[data-testid='sc-pager']").TextContent.Trim());
        Assert.Contains("SOOJIN", panel.Find(".sc-name").TextContent);
        Assert.True(panel.Find("[data-testid='sc-next']").HasAttribute("disabled"));
    }

    [Fact]
    public void ANoteReadsOnlyYouAndCarriesNoVote()
    {
        Marks(Mark(66.2m, "Breathe before the hold.", note: true, isAuthor: true));

        var panel = Render();

        Assert.Equal("Only you", panel.Find("[data-testid='sc-note']").TextContent.Trim());
        Assert.Contains("sc-chip-you", panel.Find("[data-testid='sc-second']").ClassName);
        Assert.Empty(panel.FindAll("[data-testid='sc-vote']"));
    }

    [Fact]
    public async Task APickTurnsThePanelIntoTheComposerAndThePostCarriesTheSecond()
    {
        Mediator.Setup(m => m.Send(It.IsAny<PostCommentCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid());
        var panel = Render();

        await panel.InvokeAsync(() => panel.Instance.OnPick(33.45m));

        // The head is the time chip, the scope chip and the ✕ — and the ✕ is last (D14/D15).
        var head = panel.Find(".sc-panel .sc-panel-head");
        Assert.Equal("0:33", panel.Find("[data-testid='sc-pick']").TextContent.Trim());
        Assert.Contains("Public", panel.Find("[data-testid='sc-scope-compose']").TextContent);
        Assert.Equal("sc-pick-cancel", head.LastElementChild!.GetAttribute("data-testid"));
        var line = panel.Find("[data-testid='sc-composer']");
        Assert.Equal("Comment on 0:33…", line.GetAttribute("placeholder"));
        // One way out: no Cancel in the foot, and no audience sentence anywhere.
        Assert.DoesNotContain(panel.FindAll(".sc-panel button"), b => b.TextContent.Trim() == "Cancel");
        Assert.DoesNotContain("Posting to", panel.Find(".sc-panel").TextContent);

        await line.InputAsync(new ChangeEventArgs { Value = "This quad is a bracket." });
        await panel.Find("[data-testid='sc-composer-submit']").ClickAsync(new MouseEventArgs());

        Mediator.Verify(m => m.Send(It.Is<PostCommentCommand>(command =>
            command.ChartId == Chart && command.AnchorAt == 33.45m && command.Audience == CommentAudience.Public &&
            command.Text == "This quad is a bracket."), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Empty(panel.FindAll("[data-testid='sc-pick']"));
    }

    [Fact]
    public async Task SwitchingTheScopeWhileWritingKeepsThePickAndRetargetsThePost()
    {
        Mediator.Setup(m => m.Send(It.IsAny<PostCommentCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid());
        var panel = Render();
        await panel.InvokeAsync(() => panel.Instance.OnPick(33.45m));

        await panel.Find("[data-testid='sc-scope-compose']").ClickAsync(new MouseEventArgs());
        await panel.Find("[data-testid='sc-scope-item-Notes']").ClickAsync(new MouseEventArgs());

        // The pick survives the switch; the chip, the placeholder and the button now say note.
        Assert.Equal("0:33", panel.Find("[data-testid='sc-pick']").TextContent.Trim());
        Assert.Contains("sc-scope-chip-you", panel.Find("[data-testid='sc-scope-compose']").ClassName);
        var line = panel.Find("[data-testid='sc-composer']");
        Assert.Equal("Note on 0:33…", line.GetAttribute("placeholder"));
        Assert.Equal("Save note", panel.Find("[data-testid='sc-composer-submit']").TextContent.Trim());
        // The strip reloaded for the new scope — one setting for bar and composer.
        Mediator.Verify(m => m.Send(It.Is<GetChartCommentMarksQuery>(q => q.Audience.IsPrivate),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);

        await line.InputAsync(new ChangeEventArgs { Value = "Breathe before the hold." });
        await panel.Find("[data-testid='sc-composer-submit']").ClickAsync(new MouseEventArgs());

        Mediator.Verify(m => m.Send(It.Is<PostCommentCommand>(command =>
            command.Audience.IsPrivate && command.AnchorAt == 33.45m), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CancellingAPickReturnsToBrowsing()
    {
        Marks(Mark(29m, "The drills start here.", "ERRLENA"));
        var panel = Render();
        await panel.InvokeAsync(() => panel.Instance.OnPick(40m));

        await panel.Find("[data-testid='sc-pick-cancel']").ClickAsync(new MouseEventArgs());

        Assert.Empty(panel.FindAll("[data-testid='sc-pick']"));
        Assert.Equal("0:29", panel.Find("[data-testid='sc-second']").TextContent.Trim());
    }

    [Fact]
    public async Task SignedOutAPickAsksToSignInInsteadOfOfferingAComposer()
    {
        CurrentUser.Setup(u => u.IsLoggedIn).Returns(false);
        var panel = Render();

        await panel.InvokeAsync(() => panel.Instance.OnPick(33.45m));

        Assert.Contains("Sign in to comment on 0:33", panel.Find("[data-testid='sc-signin']").TextContent);
        Assert.Empty(panel.FindAll("[data-testid='sc-composer']"));
        Assert.Empty(panel.FindAll("[data-testid='sc-scope-compose']"));
    }

    [Fact]
    public async Task OpenThreadHandsTheHostTheCommentAndItsScope()
    {
        var note = Mark(66.2m, "Breathe before the hold.", note: true, isAuthor: true);
        Marks(note);
        (Guid CommentId, CommentAudience Audience)? opened = null;
        var panel = Render(p => p.Add(c => c.OnOpenThread,
            EventCallback.Factory.Create<(Guid, CommentAudience)>(this, args => opened = args)));

        await panel.Find("[data-testid='sc-thread']").ClickAsync(new MouseEventArgs());

        Assert.NotNull(opened);
        Assert.Equal(note.Id, opened!.Value.CommentId);
        Assert.True(opened.Value.Audience.IsPrivate);
    }
}
