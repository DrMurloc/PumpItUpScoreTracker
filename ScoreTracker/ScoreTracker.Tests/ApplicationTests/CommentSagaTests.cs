using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using ScoreTracker.ChartComments.Application;
using ScoreTracker.ChartComments.Contracts;
using ScoreTracker.ChartComments.Contracts.Commands;
using ScoreTracker.ChartComments.Contracts.Queries;
using ScoreTracker.ChartComments.Domain;
using ScoreTracker.Communities.Contracts.Queries;
using ScoreTracker.CommunityTools.Contracts;
using ScoreTracker.CommunityTools.Contracts.Queries;
using ScoreTracker.Domain.Exceptions;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.TestHelpers;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class CommentSagaTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 8, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid ChartId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid ClubId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private readonly Mock<ICommentRepository> _comments = new();
    private readonly Mock<ICommentConsentRepository> _consents = new();
    private readonly Mock<ICurrentUserAccessor> _currentUser = new();
    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<IUserReader> _users = new();
    private readonly User _viewer = new(Guid.NewGuid(), Name.From("ERRLENA"), true, null,
        new Uri("https://example.com/a.png"), Name.From("US"));

    /// <summary>The site admin, whose id User.IsAdmin computes against — no flag, no seed row.</summary>
    private static readonly User Admin = new(Guid.Parse("E38954C4-B1B1-418A-93F6-C4B25C98B713"),
        Name.From("DrMurloc"), true, null, new Uri("https://example.com/d.png"), Name.From("US"));

    public CommentSagaTests()
    {
        _currentUser.SetupGet(c => c.IsLoggedIn).Returns(true);
        _currentUser.SetupGet(c => c.User).Returns(() => _viewer);
        _mediator.Setup(m => m.Send(It.IsAny<GetMyCommunitiesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<CommunityOverviewRecord>());
        _mediator.Setup(m => m.Send(It.IsAny<GetPublicToolsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<PublicToolRecord>());
        _users.Setup(u => u.GetUsers(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<User>());
    }

    private CommentSaga Subject()
    {
        return new CommentSaga(_comments.Object, _consents.Object, _currentUser.Object,
            FakeDateTime.At(Now).Object, _mediator.Object, _users.Object,
            new MemoryCache(new MemoryCacheOptions()));
    }

    private void Communities(params (string Name, Guid Id, bool Regional)[] communities)
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetMyCommunitiesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(communities.Select(c =>
                new CommunityOverviewRecord(Name.From(c.Name), CommunityPrivacyType.Public, 3, c.Regional,
                    c.Id)).ToArray());
    }

    // ----- the scope rail ----------------------------------------------------------------------

    [Fact]
    public async Task TheRailIsPublicThenNotesThenYourClubs()
    {
        Communities(("Murloc Lab", ClubId, false));

        var scopes = await Subject().Handle(new GetMyCommentScopesQuery(), CancellationToken.None);

        Assert.Collection(scopes,
            s => Assert.Equal(CommentAudienceKind.Public, s.Audience.Kind),
            s => Assert.Equal(CommentAudienceKind.Private, s.Audience.Kind),
            s =>
            {
                Assert.Equal(ClubId, s.Audience.CommunityId);
                Assert.Equal("Murloc Lab", s.Label.ToString());
            });
    }

    [Fact]
    public async Task OwnerlessBoardsAreNotAudiences()
    {
        // Regional communities carry no roles, so a comment there would have no moderator — and
        // World is flagged IsRegional = 0, which is why the name check is load-bearing rather
        // than belt-and-braces.
        Communities(("Japan", Guid.NewGuid(), true), ("World", Guid.NewGuid(), false),
            ("NYC Pump", ClubId, false));

        var scopes = await Subject().Handle(new GetMyCommentScopesQuery(), CancellationToken.None);

        Assert.Equal(new[] { "Public", "Notes", "NYC Pump" }, scopes.Select(s => s.Label.ToString()));
    }

    [Fact]
    public async Task SignedOutThereIsNowhereToPost()
    {
        _currentUser.SetupGet(c => c.IsLoggedIn).Returns(false);

        Assert.Empty(await Subject().Handle(new GetMyCommentScopesQuery(), CancellationToken.None));
    }

    // ----- posting -----------------------------------------------------------------------------

    [Fact]
    public async Task PostingToACommunityYouAreNotInIsRefused()
    {
        Communities(("Murloc Lab", ClubId, false));

        await Assert.ThrowsAsync<CommentNotAllowedException>(() => Subject().Handle(
            new PostCommentCommand(ChartId, CommentAudience.Community(Guid.NewGuid()), "hello"),
            CancellationToken.None));

        _comments.Verify(c => c.Save(It.IsAny<Comment>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PostingToAClubYouAreInSavesIt()
    {
        Communities(("Murloc Lab", ClubId, false));

        await Subject().Handle(new PostCommentCommand(ChartId, CommentAudience.Community(ClubId), "hi"),
            CancellationToken.None);

        _comments.Verify(c => c.Save(It.Is<Comment>(comment =>
            comment.Audience.CommunityId == ClubId && comment.ChartId == ChartId), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ANoteNeedsNoMembershipAndNoAgreement()
    {
        await Subject().Handle(new PostCommentCommand(ChartId, CommentAudience.Private, "left foot"),
            CancellationToken.None);

        _comments.Verify(c => c.Save(It.Is<Comment>(comment => comment.Audience.IsPrivate),
            It.IsAny<CancellationToken>()), Times.Once);

        var consent = await Subject().Handle(new GetCommentConsentQuery(CommentAudience.Private),
            CancellationToken.None);
        Assert.False(consent.NeedsAnything);
    }

    // ----- replies -----------------------------------------------------------------------------

    [Fact]
    public async Task ReplyingToAReplyTargetsTheRoot()
    {
        var root = Comment.Post(ChartId, Guid.NewGuid(), CommentAudience.Public, "root", Now);
        var reply = Comment.Reply(root, Guid.NewGuid(), "reply", Now);
        _comments.Setup(c => c.GetById(reply.Id, It.IsAny<CancellationToken>())).ReturnsAsync(reply);
        _comments.Setup(c => c.GetById(root.Id, It.IsAny<CancellationToken>())).ReturnsAsync(root);

        await Subject().Handle(new ReplyToCommentCommand(reply.Id, "me too"), CancellationToken.None);

        _comments.Verify(c => c.Save(It.Is<Comment>(saved => saved.ParentCommentId == root.Id),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ----- editing -----------------------------------------------------------------------------

    [Fact]
    public async Task AnEditWritesTheRevisionBeforeTheComment()
    {
        var comment = Comment.Post(ChartId, _viewer.Id, CommentAudience.Public, "before", Now);
        _comments.Setup(c => c.GetById(comment.Id, It.IsAny<CancellationToken>())).ReturnsAsync(comment);

        await Subject().Handle(new EditCommentCommand(comment.Id, "after"), CancellationToken.None);

        _comments.Verify(c => c.WriteRevision(comment.Id, "before", Now, It.IsAny<CancellationToken>()),
            Times.Once);
        _comments.Verify(c => c.Save(It.Is<Comment>(saved => saved.Text == "after"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ----- moderation ---------------------------------------------------------------------------
    //
    // The shield is drawn from ViewerMayModerate, and ViewerMayModerate is computed here — so
    // these are the tests that decide who can moderate. A component test can only confirm the UI
    // honours the flag; nothing below the saga sets it.

    [Fact]
    public async Task OnlyTheSiteAdminRemoves()
    {
        var comment = Comment.Post(ChartId, Guid.NewGuid(), CommentAudience.Public, "words", Now);
        _comments.Setup(c => c.GetById(comment.Id, It.IsAny<CancellationToken>())).ReturnsAsync(comment);

        await Assert.ThrowsAsync<CommentNotAllowedException>(
            () => Subject().Handle(new RemoveCommentCommand(comment.Id), CancellationToken.None));

        _comments.Verify(c => c.Save(It.IsAny<Comment>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SignedOutRemovesNothing()
    {
        _currentUser.SetupGet(c => c.IsLoggedIn).Returns(false);
        var comment = Comment.Post(ChartId, Guid.NewGuid(), CommentAudience.Public, "words", Now);
        _comments.Setup(c => c.GetById(comment.Id, It.IsAny<CancellationToken>())).ReturnsAsync(comment);

        await Assert.ThrowsAsync<CommentNotAllowedException>(
            () => Subject().Handle(new RemoveCommentCommand(comment.Id), CancellationToken.None));

        _comments.Verify(c => c.Save(It.IsAny<Comment>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TheSiteAdminRemovesAndTheRowNamesThem()
    {
        _currentUser.SetupGet(c => c.User).Returns(Admin);
        var comment = Comment.Post(ChartId, Guid.NewGuid(), CommentAudience.Public, "words", Now);
        _comments.Setup(c => c.GetById(comment.Id, It.IsAny<CancellationToken>())).ReturnsAsync(comment);

        await Subject().Handle(new RemoveCommentCommand(comment.Id), CancellationToken.None);

        _comments.Verify(c => c.Save(It.Is<Comment>(saved =>
            saved.IsDeleted && saved.DeletedByUserId == Admin.Id), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(false, false)] // an ordinary reader, on a public comment
    [InlineData(true, true)]   // the site admin, on the same one
    public async Task TheShieldIsDrawnForTheSiteAdminAndNobodyElse(bool asAdmin, bool expected)
    {
        if (asAdmin) _currentUser.SetupGet(c => c.User).Returns(Admin);
        SetupRows(Row(Guid.NewGuid()));

        var record = Assert.Single((await Subject().Handle(
            new GetChartCommentsQuery(ChartId, CommentAudience.Public), CancellationToken.None)).Roots);

        Assert.Equal(expected, record.ViewerMayModerate);
    }

    [Fact]
    public async Task ACommunityAdminHoldsNoShieldYet()
    {
        // Community moderation is a later slice. Until ModerateComments exists, a club's own admin
        // has exactly the powers a member does — asserted rather than assumed, because the day the
        // flag lands this test is what says the plumbing changed on purpose.
        Communities(("Murloc Lab", ClubId, false));
        SetupRows(Row(Guid.NewGuid()));

        var record = Assert.Single((await Subject().Handle(
            new GetChartCommentsQuery(ChartId, CommentAudience.Community(ClubId)),
            CancellationToken.None)).Roots);

        Assert.False(record.ViewerMayModerate);
    }

    [Fact]
    public async Task SignedOutNobodyHoldsAShield()
    {
        _currentUser.SetupGet(c => c.IsLoggedIn).Returns(false);
        SetupRows(Row(Guid.NewGuid()));

        var record = Assert.Single((await Subject().Handle(
            new GetChartCommentsQuery(ChartId, CommentAudience.Public), CancellationToken.None)).Roots);

        Assert.False(record.ViewerMayModerate);
        Assert.False(record.ViewerIsAuthor);
    }

    [Fact]
    public async Task EvenTheSiteAdminHoldsNoShieldOnTheirOwnNotes()
    {
        // Notes are unmoderated by anybody, including the person who owns the site. The aggregate
        // refuses it too; this is the half that stops the control being drawn in the first place.
        _currentUser.SetupGet(c => c.User).Returns(Admin);
        SetupRows(Row(Admin.Id));

        var record = Assert.Single((await Subject().Handle(
            new GetChartCommentsQuery(ChartId, CommentAudience.Private), CancellationToken.None)).Roots);

        Assert.False(record.ViewerMayModerate);
    }

    [Fact]
    public async Task ANoteIsRefusedToAModeratorEvenIfOneIsSomehowReached()
    {
        // Belt to the query filter's braces: if a note ever did reach a moderator's hand, the
        // aggregate still will not let them take it down.
        _currentUser.SetupGet(c => c.User).Returns(Admin);
        var note = Comment.Post(ChartId, Guid.NewGuid(), CommentAudience.Private, "left foot", Now);
        _comments.Setup(c => c.GetById(note.Id, It.IsAny<CancellationToken>())).ReturnsAsync(note);

        await Assert.ThrowsAsync<CommentNotAllowedException>(
            () => Subject().Handle(new RemoveCommentCommand(note.Id), CancellationToken.None));
    }

    [Fact]
    public async Task AModeratorStillCannotEditSomebodyElsesWords()
    {
        _currentUser.SetupGet(c => c.User).Returns(Admin);
        var comment = Comment.Post(ChartId, Guid.NewGuid(), CommentAudience.Public, "words", Now);
        _comments.Setup(c => c.GetById(comment.Id, It.IsAny<CancellationToken>())).ReturnsAsync(comment);

        await Assert.ThrowsAsync<CommentNotAllowedException>(
            () => Subject().Handle(new EditCommentCommand(comment.Id, "fixed that"), CancellationToken.None));
    }

    [Fact]
    public async Task AModeratorCannotReadSomebodyElsesDraftText()
    {
        // The edit query is the one place raw comment text leaves the vertical. Holding the
        // strongest hand on the site does not open it.
        _currentUser.SetupGet(c => c.User).Returns(Admin);
        var comment = Comment.Post(ChartId, Guid.NewGuid(), CommentAudience.Public, "words", Now);
        _comments.Setup(c => c.GetById(comment.Id, It.IsAny<CancellationToken>())).ReturnsAsync(comment);

        Assert.Null(await Subject().Handle(new GetMyCommentTextQuery(comment.Id), CancellationToken.None));
    }

    // ----- consent ------------------------------------------------------------------------------

    [Fact]
    public async Task TheTermsAreAskedOnceAndAgainWhenTheyChange()
    {
        _consents.Setup(c => c.GetFor(_viewer.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CommentConsent?)null);
        Assert.True((await Subject().Handle(new GetCommentConsentQuery(CommentAudience.Public),
            CancellationToken.None)).NeedsTerms);

        _consents.Setup(c => c.GetFor(_viewer.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommentConsent(Now, CommentSaga.TermsVersion, null));
        Assert.False((await Subject().Handle(new GetCommentConsentQuery(CommentAudience.Public),
            CancellationToken.None)).NeedsTerms);

        _consents.Setup(c => c.GetFor(_viewer.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommentConsent(Now, CommentSaga.TermsVersion - 1, null));
        Assert.True((await Subject().Handle(new GetCommentConsentQuery(CommentAudience.Public),
            CancellationToken.None)).NeedsTerms);
    }

    [Fact]
    public async Task TheIdentityConsentIsCollectedWhenItBecomesTrueAndNotBefore()
    {
        var privateProfile = _viewer with { IsPublic = false };
        _currentUser.SetupGet(c => c.User).Returns(privateProfile);
        _consents.Setup(c => c.GetFor(privateProfile.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommentConsent(Now, CommentSaga.TermsVersion, null));

        // Posting to a club first shows one checkbox; the identity one appears later, in public.
        Assert.False((await Subject().Handle(new GetCommentConsentQuery(CommentAudience.Community(ClubId)),
            CancellationToken.None)).NeedsPublicIdentityConsent);
        Assert.True((await Subject().Handle(new GetCommentConsentQuery(CommentAudience.Public),
            CancellationToken.None)).NeedsPublicIdentityConsent);
    }

    [Fact]
    public async Task APublicProfileIsNeverAskedAboutItsIdentity()
    {
        _consents.Setup(c => c.GetFor(_viewer.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommentConsent(Now, CommentSaga.TermsVersion, null));

        Assert.False((await Subject().Handle(new GetCommentConsentQuery(CommentAudience.Public),
            CancellationToken.None)).NeedsPublicIdentityConsent);
    }

    // ----- reading ------------------------------------------------------------------------------

    [Fact]
    public async Task SignedOutTheNotesScopeIsEmptyRatherThanEverybodys()
    {
        _currentUser.SetupGet(c => c.IsLoggedIn).Returns(false);

        var page = await Subject().Handle(new GetChartCommentsQuery(ChartId, CommentAudience.Private),
            CancellationToken.None);

        Assert.Empty(page.Roots);
        _comments.Verify(c => c.GetForChart(It.IsAny<Guid>(), It.IsAny<CommentAudience>(),
            It.IsAny<Guid>(), It.IsAny<CommentSort>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ADeletedRootNobodyAnsweredIsNotAHeadstone()
    {
        var kept = Row(Guid.NewGuid(), deleted: true);
        var dropped = Row(Guid.NewGuid(), deleted: true);
        var reply = Row(Guid.NewGuid(), parent: kept.Id);
        SetupRows(kept, reply, dropped);

        var page = await Subject().Handle(new GetChartCommentsQuery(ChartId, CommentAudience.Public),
            CancellationToken.None);

        var root = Assert.Single(page.Roots);
        Assert.Equal(kept.Id, root.Id);
        Assert.Equal(CommentDeletion.ByAuthor, root.Deletion);
        Assert.Empty(root.Body);
        Assert.Single(root.Replies);
    }

    [Fact]
    public async Task ADeletedReplyRendersNothingAtAll()
    {
        // It is holding nothing open, so a stub for it would be a headstone in the middle of
        // somebody else's conversation.
        var root = Row(Guid.NewGuid());
        SetupRows(root, Row(Guid.NewGuid(), parent: root.Id, deleted: true),
            Row(Guid.NewGuid(), parent: root.Id));

        var record = Assert.Single((await Subject().Handle(
            new GetChartCommentsQuery(ChartId, CommentAudience.Public), CancellationToken.None)).Roots);

        Assert.Single(record.Replies);
        Assert.All(record.Replies, reply => Assert.Null(reply.Deletion));
    }

    [Fact]
    public async Task AThreadWhoseEveryCommentIsDeletedDisappearsEntirely()
    {
        // The stub exists to hold a surviving reply somewhere. Once the last one goes there is
        // nothing left to hold, so the thread goes with it rather than leaving a marker for a
        // conversation nobody can read.
        var root = Row(Guid.NewGuid(), deleted: true);
        SetupRows(root, Row(Guid.NewGuid(), parent: root.Id, deleted: true),
            Row(Guid.NewGuid(), parent: root.Id, deleted: true));

        var page = await Subject().Handle(new GetChartCommentsQuery(ChartId, CommentAudience.Public),
            CancellationToken.None);

        Assert.Empty(page.Roots);
    }

    [Fact]
    public async Task ATombstonedRootSaysTheAccountIsGone()
    {
        var tombstoned = Row(Guid.NewGuid(), deleted: true) with { UserId = Guid.Empty, Text = "" };
        var reply = Row(Guid.NewGuid(), parent: tombstoned.Id);
        SetupRows(tombstoned, reply);

        var root = Assert.Single((await Subject().Handle(
            new GetChartCommentsQuery(ChartId, CommentAudience.Public), CancellationToken.None)).Roots);

        Assert.Equal(CommentDeletion.ByDeletedAccount, root.Deletion);
        Assert.Null(root.AuthorId);
    }

    [Fact]
    public async Task ANoteNeverCarriesAShieldEvenForTheAdmin()
    {
        var admin = new User(Guid.Parse("E38954C4-B1B1-418A-93F6-C4B25C98B713"), Name.From("DrMurloc"),
            true, null, new Uri("https://example.com/d.png"), Name.From("US"));
        _currentUser.SetupGet(c => c.User).Returns(admin);
        var note = Row(admin.Id);
        SetupRows(note);

        var record = Assert.Single((await Subject().Handle(
            new GetChartCommentsQuery(ChartId, CommentAudience.Private), CancellationToken.None)).Roots);

        Assert.True(admin.IsAdmin);
        Assert.False(record.ViewerMayModerate);
    }

    private CommentRow Row(Guid userId, Guid? parent = null, bool deleted = false)
    {
        return new CommentRow(Guid.NewGuid(), ChartId, userId, parent, "some words", Now, null,
            deleted ? Now : null, deleted ? userId : null, 0, false);
    }

    private void SetupRows(params CommentRow[] rows)
    {
        _comments.Setup(c => c.GetForChart(ChartId, It.IsAny<CommentAudience>(), It.IsAny<Guid>(),
                It.IsAny<CommentSort>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(rows);
        _comments.Setup(c => c.CountRoots(ChartId, It.IsAny<CommentAudience>(), It.IsAny<Guid>(),
            It.IsAny<CancellationToken>())).ReturnsAsync(rows.Count(r => r.ParentCommentId == null));
    }
}
