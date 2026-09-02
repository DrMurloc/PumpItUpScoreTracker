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
///     The sticky panel at the bottom of the step chart (docs/design/step-chart-comments D4). The
///     module's side of the seam is exercised on a browser harness; these pin what the panel
///     renders for each state the module can put it in — browsing a comment, paging a stack,
///     a note, the composer after a pick, and the signed-out reader — through the JSInvokable
///     entry points the module calls, with no JS at all.
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
        Marks();
    }

    private void Marks(params CommentRecord[] marks)
    {
        Mediator.Setup(m => m.Send(It.IsAny<GetChartCommentMarksQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(marks);
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
    public void TheEmptyStateTeachesTheGesture()
    {
        var panel = Render();

        var empty = panel.Find("[data-testid='sc-empty']");
        Assert.Contains("double-click a spot", empty.TextContent);
        Assert.Contains("double-tap a spot", empty.TextContent);
    }

    [Fact]
    public void ASignedOutReaderGetsTheQuietEmptyState()
    {
        CurrentUser.Setup(u => u.IsLoggedIn).Returns(false);

        var empty = Render().Find("[data-testid='sc-empty']");

        Assert.DoesNotContain("double-click", empty.TextContent);
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
        // The last comment on the chart: nothing further to step to.
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

        Assert.Equal("0:33", panel.Find("[data-testid='sc-pick']").TextContent.Trim());
        var line = panel.Find("[data-testid='sc-composer']");
        Assert.Equal("Comment on 0:33…", line.GetAttribute("placeholder"));
        Assert.Contains("Posting to Public as ERRLENA", panel.Find(".sc-posting").TextContent);

        await line.InputAsync(new ChangeEventArgs { Value = "This quad is a bracket." });
        await panel.Find("[data-testid='sc-composer-submit']").ClickAsync(new MouseEventArgs());

        Mediator.Verify(m => m.Send(It.Is<PostCommentCommand>(command =>
            command.ChartId == Chart && command.AnchorAt == 33.45m && command.Audience == CommentAudience.Public &&
            command.Text == "This quad is a bracket."), It.IsAny<CancellationToken>()), Times.Once);
        // Posted: the composer is gone and the panel is back to browsing.
        Assert.Empty(panel.FindAll("[data-testid='sc-pick']"));
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
        // A note lives in the Notes scope, so the host opens standing there.
        Assert.True(opened.Value.Audience.IsPrivate);
    }
}
