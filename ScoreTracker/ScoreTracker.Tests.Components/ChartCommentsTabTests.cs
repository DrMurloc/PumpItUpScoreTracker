using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using Bunit;
using MediatR;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using MudBlazor.Services;
using ScoreTracker.ChartComments.Contracts;
using ScoreTracker.ChartComments.Contracts.Queries;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web;
using ScoreTracker.Web.Components.ChartComments;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The Comments tab. What is asserted here is which controls a given reader gets, because
///     in this feature the controls <em>are</em> the permission model — a shield nobody else
///     renders, a ⋯ only on your own words, and a Notes scope that looks nothing like a thread.
/// </summary>
public sealed class ChartCommentsTabTests : TestContext
{
    private static readonly Guid Chart = Guid.Parse("cccccccc-0000-0000-0000-00000000000c");
    private static readonly Guid Club = Guid.Parse("aaaaaaaa-0000-0000-0000-00000000000a");

    private readonly Mock<ICurrentUserAccessor> _currentUser = new();
    private readonly Mock<IMediator> _mediator = new();

    private readonly User _viewer = new(Guid.NewGuid(), Name.From("ERRLENA"), true, null,
        new Uri("https://example.com/a.png"), Name.From("US"));

    public ChartCommentsTabTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices(o => o.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddSingleton(_mediator.Object);
        Services.AddSingleton(_currentUser.Object);
        Services.AddSingleton<IStringLocalizer<App>>(new PassThroughLocalizer());

        _currentUser.Setup(u => u.IsLoggedIn).Returns(true);
        _currentUser.SetupGet(u => u.User).Returns(() => _viewer);
        Scopes(new CommentScopeRecord(CommentAudience.Public, Name.From("Public")),
            new CommentScopeRecord(CommentAudience.Private, Name.From("Notes")),
            new CommentScopeRecord(CommentAudience.Community(Club), Name.From("Murloc Lab")));
        Consent(false, false);
        Page();
    }

    private void Scopes(params CommentScopeRecord[] scopes)
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetMyCommentScopesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(scopes);
    }

    private void Consent(bool needsTerms, bool needsIdentity)
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetCommentConsentQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommentConsentRecord(needsTerms, needsIdentity));
    }

    private void Page(params CommentRecord[] roots)
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetChartCommentsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommentPageRecord(roots, roots.Length, false));
    }

    private static CommentRecord Comment(string text, bool isAuthor = false, bool mayModerate = false,
        bool untrustedLink = false, CommentDeletion? deletion = null, params CommentRecord[] replies)
    {
        var body = untrustedLink
            ? new[] { CommentSpan.OfLink("https://stepcharts.example.net/x", false) }
            : new[] { CommentSpan.OfText(text) };

        return new CommentRecord(Guid.NewGuid(), Chart, isAuthor ? Guid.NewGuid() : Guid.NewGuid(),
            Name.From("TUSA"), Name.From("KR"), new Uri("https://example.com/t.png"),
            deletion == null ? body : Array.Empty<CommentSpan>(), 3, false, isAuthor, mayModerate,
            DateTimeOffset.UtcNow.AddDays(-1), null, deletion, replies);
    }

    private IRenderedComponent<ChartCommentsTab> Render()
    {
        return RenderComponent<ChartCommentsTab>(p => p.Add(c => c.ChartId, Chart).Add(c => c.Active, true));
    }

    [Fact]
    public void TheRailIsTheAudiencePicker()
    {
        var chips = Render().FindAll(".cld-chip");

        Assert.Equal(new[] { "Public", "Notes", "Murloc Lab" }, chips.Select(c => c.TextContent.Trim()));
    }

    [Fact]
    public async Task TheNotesScopeDropsTheAvatarAndEveryConversationControl()
    {
        Page(Comment("left foot leads the drill"), Comment("bpm ramps at 1:40"));
        var page = Render();

        await page.Find("[data-testid='cmt-scope-Notes']").ClickAsync(new MouseEventArgs());

        // Notes rows are a different shape entirely: no author column, nothing to vote on,
        // nobody to reply to.
        Assert.Equal(2, page.FindAll(".cmt-note").Count);
        Assert.Empty(page.FindAll(".cmt-note .cmt-avatar"));
        Assert.Empty(page.FindAll(".cmt-foot"));
        // With no votes there is only one order, so there is no control offering two.
        Assert.Empty(page.FindAll("[data-testid='cmt-sort']"));
    }

    [Fact]
    public async Task ANoteComposerPromisesPrivacyRatherThanNamingYou()
    {
        var page = Render();

        await page.Find("[data-testid='cmt-scope-Notes']").ClickAsync(new MouseEventArgs());
        page.Find(".cmt-line").Focus();

        Assert.Contains("Only you can see this", page.Markup);
        Assert.DoesNotContain("Posting to", page.Markup);
    }

    [Fact]
    public void ThePublicComposerSaysWhereItIsGoingAndUnderWhoseName()
    {
        var page = Render();

        page.Find(".cmt-line").Focus();

        Assert.Contains("Posting to Public as ERRLENA", page.Markup);
    }

    [Fact]
    public void SomebodyElsesCommentCarriesNoMenuAtAll()
    {
        // Report and the community shield arrive with moderation, so in this slice a normal
        // reader's row on somebody else's words has ▲ and Reply and nothing more.
        Page(Comment("not mine"));
        var page = Render();

        Assert.NotEmpty(page.FindAll(".cmt-foot .cmt-act"));
        Assert.Empty(page.FindAll("[data-testid^='own-']"));
        Assert.DoesNotContain("Shield", page.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ASignedOutReaderStillReadsButGetsNoComposer()
    {
        _currentUser.Setup(u => u.IsLoggedIn).Returns(false);
        Page(Comment("public and readable"));

        var page = Render();

        Assert.Contains("public and readable", page.Markup);
        Assert.Empty(page.FindAll(".cmt-line"));
    }

    [Fact]
    public void TheRulesCardTakesTheComposersPlaceUntilItIsAccepted()
    {
        Consent(true, false);

        var page = Render();

        // In place of the composer, not over it: a MudDialog inside the details dialog orphans
        // its scrim, and the reader would be left with a page they cannot click.
        Assert.Contains("DrMurloc's Rules for Comments", page.Markup);
        Assert.Empty(page.FindAll(".cmt-line"));
    }

    [Fact]
    public async Task AnUnknownHostIsWarnedAboutBeforeItIsOpened()
    {
        Page(Comment("look here", untrustedLink: true));
        var page = Render();

        Assert.Empty(page.FindAll("[data-testid='cmt-interstitial']"));

        await page.Find(".cmt-link-untrusted").ClickAsync(new MouseEventArgs());

        // The parsed host, never the link text — the text is the author's to choose.
        var interstitial = page.Find("[data-testid='cmt-interstitial']");
        Assert.Contains("stepcharts.example.net", interstitial.TextContent);
    }

    [Fact]
    public async Task EditOpensAComposerHoldingTheWordsAsTheyStand()
    {
        // The text arrives through its own author-gated query rather than riding on the render
        // record, so this also pins that the tab actually asks for it.
        _mediator.Setup(m => m.Send(It.IsAny<GetMyCommentTextQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("the drill at 2:01");
        var mine = Comment("the drill at 2:01", isAuthor: true);
        Page(mine);
        var page = Render();

        // Invoked rather than clicked: the ⋯ is a MudMenu, which is a popover bUnit cannot open.
        // What is worth pinning is the wiring behind it, which used to set a field nobody read.
        var row = page.FindComponent<CommentRow>();
        await page.InvokeAsync(() => row.Instance.OnEdit.InvokeAsync(mine));

        var composer = page.Find("[data-testid='cmt-edit-composer']");
        Assert.Equal("the drill at 2:01", composer.GetAttribute("value"));
    }

    [Fact]
    public void AFocusedCommentIsMarkedRatherThanScrolledTo()
    {
        var comment = Comment("the reported one");
        Page(comment);

        var page = RenderComponent<ChartCommentsTab>(p => p
            .Add(c => c.ChartId, Chart).Add(c => c.Active, true)
            .Add(c => c.FocusCommentId, comment.Id));

        Assert.Single(page.FindAll(".cmt-focused"));
    }

    [Fact]
    public void AStubKeepsTheThreadsShapeAndSaysWhoTookItDown()
    {
        Page(Comment("gone", deletion: CommentDeletion.ByModerator, replies: Comment("still here")));

        var page = Render();

        Assert.Contains("Removed by the site admin", page.Markup);
        Assert.Contains("still here", page.Markup);
    }

    [Fact]
    public void ATombstonedRootSaysTheAccountIsGoneRatherThanNamingAnybody()
    {
        Page(Comment("gone", deletion: CommentDeletion.ByDeletedAccount, replies: Comment("still here")));

        Assert.Contains("Comment from a deleted user", Render().Markup);
    }

    /// <summary>Returns the key, which is the English copy — enough to assert on.</summary>
    private sealed class PassThroughLocalizer : IStringLocalizer<App>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] =>
            new(name, string.Format(name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
        {
            return Array.Empty<LocalizedString>();
        }
    }
}
