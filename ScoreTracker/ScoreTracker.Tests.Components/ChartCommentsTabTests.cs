using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using Bunit;
using MediatR;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using MudBlazor;
using MudBlazor.Services;
using ScoreTracker.ChartComments.Contracts;
using ScoreTracker.ChartComments.Contracts.Commands;
using ScoreTracker.ChartComments.Contracts.Queries;
using ScoreTracker.Communities.Contracts.Queries;
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
        Services.AddSingleton(Mock.Of<ScoreTracker.Web.Services.Contracts.IUiSettingsAccessor>());

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
    public async Task ANoteComposerDropsTheAvatarColumnAndNotJustTheAvatar()
    {
        // Shipped broken: the grid declares two columns, the avatar was suppressed, and the field
        // landed in the 28px track — one character wide, buttons stacked underneath it.
        var page = Render();

        await page.Find("[data-testid='cmt-scope-Notes']").ClickAsync(new MouseEventArgs());

        var composer = page.Find(".cmt-compose");
        Assert.Contains("cmt-compose-bare", composer.ClassName);
        Assert.Empty(composer.QuerySelectorAll(".cmt-avatar"));
    }

    [Fact]
    public void ThePublicComposerKeepsItsAvatarColumn()
    {
        var composer = Render().Find(".cmt-compose");

        Assert.DoesNotContain("cmt-compose-bare", composer.ClassName);
        Assert.NotEmpty(composer.QuerySelectorAll(".cmt-avatar"));
    }

    [Fact]
    public void ThePublicComposerSaysWhereItIsGoingAndUnderWhoseName()
    {
        var page = Render();

        page.Find(".cmt-line").Focus();

        Assert.Contains("Posting to Public as ERRLENA", page.Markup);
    }

    [Fact]
    public void TheAdminIsToldWhyTheTabIsThereWhileTheFlagIsOff()
    {
        // Seeing the tab on a local run reads as "the flag is on" — it usually is not, it is the
        // IsAdmin half of the gate, and a prod-synced local database means the owner logs in as
        // himself without thinking about it.
        var page = RenderComponent<ChartCommentsTab>(p => p
            .Add(c => c.ChartId, Chart).Add(c => c.Active, true).Add(c => c.AdminPreview, true));

        Assert.NotEmpty(page.FindAll("[data-testid='cmt-admin-preview']"));
    }

    [Fact]
    public void OnceTheFlagIsOnNobodyIsToldAnything()
    {
        Assert.Empty(Render().FindAll("[data-testid='cmt-admin-preview']"));
    }

    // ----- who gets which control ---------------------------------------------------------------
    //
    // The saga decides ViewerMayModerate (CommentSagaTests owns that half). These pin the other
    // half: that the row draws the shield when and only when it is told to, and that nothing else
    // on the row is a way into a moderation action.

    [Fact]
    public void SomebodyElsesCommentCarriesReportAndOnlyReport()
    {
        // The flag-day counterpart of SomebodyElsesCommentCarriesNoMenuAtAll: moderation exists
        // now, so a signed-in reader's ⋯ on somebody else's words carries Report — while Edit
        // and Delete stay on your own rows, and no shield renders without the record saying so.
        Page(Comment("not mine"));
        var page = Render();

        Assert.NotEmpty(page.FindAll(".cmt-foot .cmt-act"));
        Assert.Empty(page.FindAll("[data-testid^='own-']"));
        Assert.Single(page.FindAll("[data-testid^='other-']"));
        Assert.DoesNotContain(page.FindComponents<MudMenu>(),
            m => m.Instance.Icon == Icons.Material.Filled.Shield);
    }

    [Fact]
    public async Task ReportingAsksInPlaceAndSendsTheReasonPicked()
    {
        var theirs = Comment("hostile words");
        Page(theirs);
        var page = Render();

        var row = page.FindComponent<CommentRow>();
        await page.InvokeAsync(() => row.Instance.OnReport.InvokeAsync(theirs));

        // The panel is under the comment — the words stay on screen while you pick — and
        // nothing is sent until Report, which stays disabled until a reason is chosen.
        page.Find($"[data-testid='report-{theirs.Id}']");
        Assert.Contains("hostile words", page.Markup);
        Assert.True(page.Find($"[data-testid='report-go-{theirs.Id}']").HasAttribute("disabled"));

        await page.Find($"[data-testid='report-reason-{theirs.Id}-HateOrDiscrimination']")
            .ChangeAsync(new ChangeEventArgs());
        await page.Find($"[data-testid='report-go-{theirs.Id}']").ClickAsync(new MouseEventArgs());

        _mediator.Verify(m => m.Send(It.Is<ReportCommentCommand>(c =>
                c.CommentId == theirs.Id && c.Reason == CommentReportReason.HateOrDiscrimination),
            It.IsAny<CancellationToken>()), Times.Once);
        Assert.Empty(page.FindAll($"[data-testid='report-{theirs.Id}']"));
    }

    [Fact]
    public async Task AScopeYouCannotPostToAlsoHidesReplyAndEdit()
    {
        // The composer-is-a-sentence treatment reaches the rows too: no Reply on anybody's
        // comment, no Edit in your own ⋯ — Delete stays, and voting stays, because a vote is
        // not content. The server refuses all three anyway; this stops the row offering a
        // composer the submit would bounce.
        Scopes(new CommentScopeRecord(CommentAudience.Public, Name.From("Public"), false),
            new CommentScopeRecord(CommentAudience.Private, Name.From("Notes")));
        var mine = Comment("my old words", isAuthor: true);
        var theirs = Comment("their words");
        Page(mine, theirs);
        var page = Render();

        Assert.Empty(page.FindAll($"[data-testid='reply-{theirs.Id}']"));
        var ownMenu = page.FindComponents<MudMenu>()
            .First(m => m.Instance.Icon == Icons.Material.Filled.MoreHoriz &&
                        m.Markup.Contains($"own-{mine.Id}"));
        await page.InvokeAsync(() => ownMenu.Find("button").ClickAsync(new MouseEventArgs()));
        Assert.DoesNotContain("Edit", ownMenu.Markup);
        // Vote is still live for their comment.
        Assert.False(page.Find($"[data-testid='vote-{theirs.Id}']").HasAttribute("disabled"));
    }

    [Fact]
    public async Task TheModerationHandoffOpensStandingInTheRightScopeWithAFocusSizedPage()
    {
        // The queue's whole promise: Open lands ON the reported comment. That takes the scope
        // (every queue row is a community comment; the default is Public, where it is not) and
        // a first page big enough that a fresh, few-votes comment cannot sort below the fold.
        var page = RenderComponent<ChartCommentsTab>(p => p
            .Add(c => c.ChartId, Chart)
            .Add(c => c.Active, true)
            .Add(c => c.FocusCommentId, Guid.NewGuid())
            .Add(c => c.InitialAudience, CommentAudience.Community(Club)));

        page.WaitForAssertion(() => _mediator.Verify(m => m.Send(It.Is<GetChartCommentsQuery>(q =>
                q.Audience == CommentAudience.Community(Club) && q.TakeRoots == 500),
            It.IsAny<CancellationToken>()), Times.Once));
    }

    [Fact]
    public void TheSiteAdminHandedAForeignClubGetsAReadOnlyModeratorChip()
    {
        // /Admin/Comments opens the site admin into a club they may not belong to. The rail has
        // no chip for it, so the tab adds one — labeled with the club's name, read-only, because
        // the open report grants a read and the admin is not a member who posts there.
        var admin = new User(Guid.Parse("E38954C4-B1B1-418A-93F6-C4B25C98B713"), Name.From("DrMurloc"),
            true, null, new Uri("https://example.com/d.png"), Name.From("US"));
        _currentUser.SetupGet(u => u.User).Returns(admin);
        var foreignClub = Guid.NewGuid();
        Scopes(new CommentScopeRecord(CommentAudience.Public, Name.From("Public")),
            new CommentScopeRecord(CommentAudience.Private, Name.From("Notes")));
        _mediator.Setup(m => m.Send(It.IsAny<GetCommunityNamesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, Name> { [foreignClub] = Name.From("Their Club") });

        var page = RenderComponent<ChartCommentsTab>(p => p
            .Add(c => c.ChartId, Chart)
            .Add(c => c.Active, true)
            .Add(c => c.InitialAudience, CommentAudience.Community(foreignClub)));

        var chips = page.FindAll(".cld-chip").Select(c => c.TextContent.Trim()).ToArray();
        Assert.Equal(new[] { "Public", "Notes", "Their Club" }, chips);
        // Standing in it, and read-only: the composer is the sentence, not a field.
        Assert.Contains("cld-chip-on", page.Find("[data-testid='cmt-scope-Their Club']").ClassName);
        page.Find("[data-testid='cmt-cannot-post']");
    }

    [Fact]
    public void AnOrdinaryReaderHandedAForeignClubGetsNoExtraChip()
    {
        // The chip is the site admin's; anyone else handed a foreign audience gets nothing extra,
        // and the read query is what refuses what they may not see.
        var foreignClub = Guid.NewGuid();
        Scopes(new CommentScopeRecord(CommentAudience.Public, Name.From("Public")),
            new CommentScopeRecord(CommentAudience.Private, Name.From("Notes")));

        var page = RenderComponent<ChartCommentsTab>(p => p
            .Add(c => c.ChartId, Chart)
            .Add(c => c.Active, true)
            .Add(c => c.InitialAudience, CommentAudience.Community(foreignClub)));

        Assert.Equal(new[] { "Public", "Notes" },
            page.FindAll(".cld-chip").Select(c => c.TextContent.Trim()).ToArray());
        _mediator.Verify(m => m.Send(It.IsAny<GetCommunityNamesQuery>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AScopeYouCannotPostToGetsASentenceInsteadOfAComposer()
    {
        // A mute (or the lock) drops CanPost; the chip stays because reading is never revoked.
        // The rules card must not show either — nobody gets walked through the terms to hit a
        // wall behind them.
        Scopes(new CommentScopeRecord(CommentAudience.Public, Name.From("Public")),
            new CommentScopeRecord(CommentAudience.Private, Name.From("Notes")),
            new CommentScopeRecord(CommentAudience.Community(Club), Name.From("Murloc Lab"), false));
        Consent(true, false);
        var page = Render();

        await page.Find("[data-testid='cmt-scope-Murloc Lab']").ClickAsync(new MouseEventArgs());

        page.Find("[data-testid='cmt-cannot-post']");
        Assert.Empty(page.FindAll("[data-testid='cmt-root-composer']"));
        Assert.Empty(page.FindAll(".cmt-rules"));

        // Public still takes a comment — the mute is that club's and only that club's. The
        // consent card shows here, because this reader still owes the terms.
        await page.Find("[data-testid='cmt-scope-Public']").ClickAsync(new MouseEventArgs());
        Assert.Empty(page.FindAll("[data-testid='cmt-cannot-post']"));
        Assert.NotEmpty(page.FindAll(".cmt-rules"));
    }

    [Fact]
    public void TheShieldRendersOnlyWhenTheRecordSaysSo()
    {
        Page(Comment("moderatable", mayModerate: true), Comment("not moderatable"));
        var page = Render();

        var shields = page.FindComponents<MudMenu>()
            .Where(m => m.Instance.Icon == Icons.Material.Filled.Shield)
            .ToArray();

        Assert.Single(shields);
    }

    [Fact]
    public void ASignedOutReaderIsOfferedNoActionOnAnybodysComment()
    {
        _currentUser.Setup(u => u.IsLoggedIn).Returns(false);
        Page(Comment("public and readable"));

        var page = Render();

        // Vote is rendered but inert, Reply is not rendered, and there is no menu of any kind.
        Assert.All(page.FindAll(".cmt-foot button"), b => Assert.True(b.HasAttribute("disabled")));
        Assert.Empty(page.FindAll("[data-testid^='own-']"));
        Assert.Empty(page.FindComponents<MudMenu>());
    }

    [Fact]
    public void ANoteRowOffersNoModerationEvenWhenTheRecordIsWrong()
    {
        // Defence in depth against a projection bug: notes are unmoderated by anybody, so the row
        // must not draw a shield on one even if handed a record claiming otherwise.
        Page(Comment("left foot", mayModerate: true));
        var page = Render();

        page.Find("[data-testid='cmt-scope-Notes']");
        var notes = RenderComponent<CommentRow>(p => p
            .Add(c => c.Comment, Comment("left foot", mayModerate: true))
            .Add(c => c.IsNote, true)
            .Add(c => c.CanInteract, true));

        Assert.DoesNotContain(notes.FindComponents<MudMenu>(),
            m => m.Instance.Icon == Icons.Material.Filled.Shield);
    }

    // ----- destructive actions confirm ----------------------------------------------------------

    [Fact]
    public async Task RemovalAsksBeforeItSendsAnything()
    {
        var theirs = Comment("someone else's words", mayModerate: true);
        Page(theirs);
        var page = Render();

        var row = page.FindComponent<CommentRow>();
        await page.InvokeAsync(() => row.Instance.OnRemove.InvokeAsync(theirs));

        // Asked, and nothing sent yet — the comment is still on screen while you decide.
        page.Find($"[data-testid='confirm-{theirs.Id}']");
        Assert.Contains("someone else's words", page.Markup);
        _mediator.Verify(m => m.Send(It.IsAny<RemoveCommentCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);

        await page.Find($"[data-testid='confirm-go-{theirs.Id}']").ClickAsync(new MouseEventArgs());

        _mediator.Verify(m => m.Send(It.Is<RemoveCommentCommand>(c => c.CommentId == theirs.Id),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CancellingAConfirmSendsNothing()
    {
        var theirs = Comment("someone else's words", mayModerate: true);
        Page(theirs);
        var page = Render();

        var row = page.FindComponent<CommentRow>();
        await page.InvokeAsync(() => row.Instance.OnRemove.InvokeAsync(theirs));
        await page.InvokeAsync(() => page.FindComponent<CommentRow>().Instance.OnCancelConfirm.InvokeAsync());

        Assert.Empty(page.FindAll($"[data-testid='confirm-{theirs.Id}']"));
        _mediator.Verify(m => m.Send(It.IsAny<RemoveCommentCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DeletingYourOwnAsksTheSameWay()
    {
        var mine = Comment("mine", isAuthor: true);
        Page(mine);
        var page = Render();

        var row = page.FindComponent<CommentRow>();
        await page.InvokeAsync(() => row.Instance.OnDelete.InvokeAsync(mine));

        page.Find($"[data-testid='confirm-{mine.Id}']");
        _mediator.Verify(m => m.Send(It.IsAny<DeleteCommentCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
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

        Assert.Contains("Removed by a moderator", page.Markup);
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
