using System;
using ScoreTracker.ChartComments.Contracts;
using ScoreTracker.ChartComments.Domain;
using ScoreTracker.Domain.Exceptions;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public sealed class CommentTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 8, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Author = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Stranger = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Chart = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid Club = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private static Comment Root(CommentAudience? audience = null)
    {
        return Comment.Post(Chart, Author, audience ?? CommentAudience.Public, "The drill at 2:01.", Now);
    }

    // ----- audience ---------------------------------------------------------------------------

    [Fact]
    public void AReplyInheritsItsRootsAudienceRatherThanTakingOne()
    {
        var root = Root(CommentAudience.Community(Club));

        var reply = Comment.Reply(root, Stranger, "Agreed.", Now);

        Assert.Equal(CommentAudienceKind.Community, reply.Audience.Kind);
        Assert.Equal(Club, reply.Audience.CommunityId);
        Assert.Equal(root.Id, reply.ParentCommentId);
    }

    [Fact]
    public void AReplyToAReplyIsRefusedRatherThanNested()
    {
        var reply = Comment.Reply(Root(), Stranger, "Agreed.", Now);

        Assert.Throws<CommentNotAllowedException>(() => Comment.Reply(reply, Author, "And also.", Now));
    }

    [Fact]
    public void AReplyToADeletedRootIsRefused()
    {
        var root = Root();
        root.DeleteByAuthor(Author, Now);

        Assert.Throws<CommentNotAllowedException>(() => Comment.Reply(root, Stranger, "Agreed.", Now));
    }

    [Fact]
    public void ACommunityAudienceNeedsACommunity()
    {
        Assert.Throws<ArgumentException>(() => CommentAudience.Community(Guid.Empty));
    }

    // ----- the four things a personal note is not --------------------------------------------

    [Fact]
    public void ANoteIsNotAConversation()
    {
        var note = Root(CommentAudience.Private);

        Assert.Throws<CommentNotAllowedException>(() => Comment.Reply(note, Author, "Also.", Now));
    }

    [Fact]
    public void ANoteIsNotVotedOn()
    {
        var note = Root(CommentAudience.Private);

        Assert.Throws<CommentNotAllowedException>(() => note.EnsureCanBeVotedOnBy(Stranger));
    }

    [Fact]
    public void ANoteIsNotModerated()
    {
        var note = Root(CommentAudience.Private);

        Assert.Throws<CommentNotAllowedException>(() => note.RemoveByModerator(Stranger, Now));
    }

    [Fact]
    public void ANoteCarriesNoSourceLanguageToTranslateFrom()
    {
        Assert.Null(Root(CommentAudience.Private).SourceLanguage);
    }

    // ----- the body ---------------------------------------------------------------------------

    [Fact]
    public void TheBodyIsStoredNormalized()
    {
        var comment = Comment.Post(Chart, Author, CommentAudience.Public, "  one\r\n\n\n\ntwo  ", Now);

        Assert.Equal("one\n\ntwo", comment.Text);
    }

    [Fact]
    public void AnEmptyBodyIsRefused()
    {
        Assert.Throws<CommentNotAllowedException>(
            () => Comment.Post(Chart, Author, CommentAudience.Public, "   \n  ", Now));
    }

    [Fact]
    public void TheCapCountsTheStoredTextAndNotWhatWasPasted()
    {
        // 500 real characters plus padding that normalization removes: this must be accepted,
        // because the cap is a promise about the comment rather than about the clipboard.
        var body = new string('x', CommentText.MaxLength);

        var comment = Comment.Post(Chart, Author, CommentAudience.Public, $"  {body}\n\n\n", Now);

        Assert.Equal(CommentText.MaxLength, comment.Text.Length);
        Assert.Throws<CommentNotAllowedException>(
            () => Comment.Post(Chart, Author, CommentAudience.Public, body + "x", Now));
    }

    [Fact]
    public void PostingSignedOutIsRefused()
    {
        Assert.Throws<CommentNotAllowedException>(
            () => Comment.Post(Chart, Guid.Empty, CommentAudience.Public, "hello", Now));
    }

    // ----- editing ----------------------------------------------------------------------------

    [Fact]
    public void AnEditReturnsWhatItReplacedSoTheRevisionCanBeWritten()
    {
        var comment = Root();
        var later = Now.AddDays(1);

        var previous = comment.Edit(Author, "The drill at 2:01, actually 2:03.", later);

        Assert.Equal("The drill at 2:01.", previous);
        Assert.Equal("The drill at 2:01, actually 2:03.", comment.Text);
        Assert.Equal(later, comment.EditedAt);
    }

    [Fact]
    public void OnlyTheAuthorEdits()
    {
        Assert.Throws<CommentNotAllowedException>(() => Root().Edit(Stranger, "Not yours.", Now));
    }

    [Fact]
    public void AModeratorCanRemoveButNotRewrite()
    {
        // "Remove and only remove". A moderator holds the strongest hand there is here and still
        // cannot put words under somebody else's name — the same refusal a stranger gets.
        var comment = Root();

        Assert.Throws<CommentNotAllowedException>(() => comment.Edit(Stranger, "Fixed that for you.", Now));

        comment.RemoveByModerator(Stranger, Now);
        Assert.True(comment.IsDeleted);
    }

    // ----- deletion ---------------------------------------------------------------------------

    [Fact]
    public void AuthorDeletionIsSoftAndNamesTheAuthor()
    {
        var comment = Root();

        comment.DeleteByAuthor(Author, Now);

        Assert.True(comment.IsDeleted);
        Assert.Equal(Author, comment.DeletedByUserId);
        Assert.False(comment.IsTombstoned);
    }

    [Fact]
    public void OnlyTheAuthorDeletesTheirOwn()
    {
        Assert.Throws<CommentNotAllowedException>(() => Root().DeleteByAuthor(Stranger, Now));
    }

    [Fact]
    public void ModeratorRemovalNamesTheModerator()
    {
        var comment = Root();

        comment.RemoveByModerator(Stranger, Now);

        Assert.True(comment.IsDeleted);
        Assert.Equal(Stranger, comment.DeletedByUserId);
    }

    [Fact]
    public void AStubIsOnlyLeftWhenAReplyHangsOffIt()
    {
        var comment = Root();
        comment.DeleteByAuthor(Author, Now);

        Assert.True(comment.LeavesStub(true));
        Assert.False(comment.LeavesStub(false));
    }

    [Fact]
    public void APurgeTombstoneKeepsNoTraceOfTheAccount()
    {
        var comment = Root();

        comment.TombstoneForPurge(Now);

        // If the row kept its UserId the decoy-account test would be right to fail it: a row still
        // keyed to a deleted account is a row the purge missed.
        Assert.Equal(Guid.Empty, comment.UserId);
        Assert.Equal(string.Empty, comment.Text);
        Assert.True(comment.IsTombstoned);
        Assert.True(comment.LeavesStub(true));
    }

    [Fact]
    public void ATombstonedCommentIsNobodysToEditOrDelete()
    {
        var comment = Root();
        comment.TombstoneForPurge(Now);

        Assert.Throws<CommentNotAllowedException>(() => comment.Edit(Guid.Empty, "hello", Now));
        Assert.Throws<CommentNotAllowedException>(() => comment.EnsureCanBeVotedOnBy(Stranger));
    }

    // ----- votes ------------------------------------------------------------------------------

    [Fact]
    public void AStrangerMayVote()
    {
        // The guard throws or it does not, so "does not" is the assertion — said out loud, because
        // a test body with no Assert in it reads as one somebody forgot to finish.
        Assert.Null(Record.Exception(() => Root().EnsureCanBeVotedOnBy(Stranger)));
    }

    [Fact]
    public void YouCannotVoteOnYourOwn()
    {
        Assert.Throws<CommentNotAllowedException>(() => Root().EnsureCanBeVotedOnBy(Author));
    }

    [Fact]
    public void ADeletedCommentTakesNoMoreVotes()
    {
        var comment = Root();
        comment.DeleteByAuthor(Author, Now);

        Assert.Throws<CommentNotAllowedException>(() => comment.EnsureCanBeVotedOnBy(Stranger));
    }
    // ----- the second a comment points at (docs/design/step-chart-comments D1, D3, D11) --------

    [Fact]
    public void ACommentMayPointAtASecondOfTheChart()
    {
        var comment = Comment.Post(Chart, Author, CommentAudience.Public, "This quad is a bracket.", Now, 33.45m);

        Assert.Equal(33.45m, comment.AnchorAt);
    }

    [Fact]
    public void ACommentAboutTheWholeChartPointsNowhere()
    {
        Assert.Null(Root().AnchorAt);
    }

    [Theory]
    [InlineData(-0.5)]
    [InlineData(3600.5)]
    public void ASecondOffTheChartIsRefused(double second)
    {
        Assert.Throws<CommentNotAllowedException>(
            () => Comment.Post(Chart, Author, CommentAudience.Public, "Nowhere.", Now, (decimal)second));
    }

    [Fact]
    public void TheEndsOfTheHourAreOnTheChart()
    {
        Assert.Equal(0m, Comment.Post(Chart, Author, CommentAudience.Public, "Start.", Now, 0m).AnchorAt);
        Assert.Equal(Comment.MaxAnchorSeconds,
            Comment.Post(Chart, Author, CommentAudience.Public, "End.", Now, Comment.MaxAnchorSeconds).AnchorAt);
    }

    [Fact]
    public void AReplyCarriesNoSecondOfItsOwn()
    {
        var root = Comment.Post(Chart, Author, CommentAudience.Public, "The drills start here.", Now, 29m);

        var reply = Comment.Reply(root, Stranger, "Right foot first.", Now);

        // A thread is about the spot its root named; the reply reads it from the root.
        Assert.Null(reply.AnchorAt);
        Assert.Equal(29m, root.AnchorAt);
    }

    [Fact]
    public void AnEditKeepsTheSecond()
    {
        var comment = Comment.Post(Chart, Author, CommentAudience.Public, "16ths.", Now, 29m);

        comment.Edit(Author, "16ths at 165.", Now.AddMinutes(1));

        Assert.Equal(29m, comment.AnchorAt);
    }

    [Fact]
    public void ANoteMayPointAtASecond()
    {
        var note = Comment.Post(Chart, Author, CommentAudience.Private, "Breathe before the hold.", Now, 66.2m);

        Assert.Equal(66.2m, note.AnchorAt);
    }

    [Fact]
    public void TheSecondSurvivesStorage()
    {
        var comment = Comment.Post(Chart, Author, CommentAudience.Public, "Bracket.", Now, 33.45m);

        var rehydrated = Comment.FromStorage(new CommentState(comment.Id, Chart, Author, CommentAudience.Public,
            null, comment.Text, Now, AnchorAt: comment.AnchorAt));

        Assert.Equal(33.45m, rehydrated.AnchorAt);
    }
}
