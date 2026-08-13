using System;
using ScoreTracker.ChartComments.Domain;
using ScoreTracker.Domain.Exceptions;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public sealed class CommentRestrictionTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 13, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Target = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Club = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Moderator = Guid.Parse("33333333-3333-3333-3333-333333333333");

    [Fact]
    public void AFreshMuteIsActiveAndKeepsItsFields()
    {
        var mute = CommentRestriction.Impose(Target, Club, Moderator, "  spam streak  ", Now);

        Assert.True(mute.IsActive);
        Assert.Equal(Target, mute.UserId);
        Assert.Equal(Moderator, mute.RestrictedByUserId);
        Assert.Equal("spam streak", mute.Reason);
    }

    [Fact]
    public void ABlankReasonStoresNull()
    {
        Assert.Null(CommentRestriction.Impose(Target, Club, Moderator, "   ", Now).Reason);
    }

    [Fact]
    public void AnEssayOfAReasonIsRejected()
    {
        Assert.Throws<CommentNotAllowedException>(() =>
            CommentRestriction.Impose(Target, Club, Moderator, new string('x', 501), Now));
    }

    [Fact]
    public void MutingYourselfIsRejected()
    {
        Assert.Throws<CommentNotAllowedException>(() =>
            CommentRestriction.Impose(Moderator, Club, Moderator, null, Now));
    }

    [Fact]
    public void LiftIsIdempotentAndKeepsTheFirstTimestamp()
    {
        var mute = CommentRestriction.Impose(Target, Club, Moderator, null, Now);

        mute.Lift(Now.AddDays(1));
        mute.Lift(Now.AddDays(9));

        Assert.False(mute.IsActive);
        Assert.Equal(Now.AddDays(1), mute.LiftedAt);
    }

    [Fact]
    public void StorageRoundTripKeepsALiftedMuteLifted()
    {
        var mute = CommentRestriction.FromStorage(new CommentRestrictionState(
            Guid.NewGuid(), Target, Club, Moderator, null, Now, Now.AddDays(2)));

        Assert.False(mute.IsActive);
    }
}
