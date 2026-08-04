using System;
using ScoreTracker.Rivals.Domain;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public sealed class RivalVisibilityPolicyTests
{
    private static readonly Guid Target = Guid.NewGuid();

    private static RivalAddCandidate Candidate(Guid? targetUserId = null, bool isPublic = false,
        bool sharesCommunity = false, bool redeemedInvite = false, bool blocked = false, bool isSelf = false) =>
        new(targetUserId, isPublic, sharesCommunity, redeemedInvite, blocked, isSelf);

    [Fact]
    public void APublicPlayerIsAddableOnTheirPublicityAlone()
    {
        var verdict = RivalVisibilityPolicy.CanAdd(Candidate(Target, isPublic: true));

        Assert.True(verdict.Allowed);
        Assert.Equal(RivalAddBasis.Public, verdict.Basis);
    }

    [Fact]
    public void APrivatePlayerInOneOfYourCommunitiesIsAddable()
    {
        var verdict = RivalVisibilityPolicy.CanAdd(Candidate(Target, sharesCommunity: true));

        Assert.True(verdict.Allowed);
        Assert.Equal(RivalAddBasis.SharedCommunity, verdict.Basis);
    }

    [Fact]
    public void APrivatePlayerWhoseCodeYouRedeemedIsAddable()
    {
        var verdict = RivalVisibilityPolicy.CanAdd(Candidate(Target, redeemedInvite: true));

        Assert.True(verdict.Allowed);
        Assert.Equal(RivalAddBasis.InviteCode, verdict.Basis);
    }

    [Fact]
    public void APrivateStrangerIsNotAddable()
    {
        var verdict = RivalVisibilityPolicy.CanAdd(Candidate(Target));

        Assert.False(verdict.Allowed);
        Assert.Equal(RivalAddRefusal.NotVisible, verdict.Refusal);
    }

    /// <summary>A board tag with no account behind it has nobody whose privacy could refuse.</summary>
    [Fact]
    public void ABoardOnlyPlayerIsAddable()
    {
        var verdict = RivalVisibilityPolicy.CanAdd(Candidate());

        Assert.True(verdict.Allowed);
        Assert.Equal(RivalAddBasis.BoardOnly, verdict.Basis);
    }

    /// <summary>
    ///     Rule 3's exception, and it needs no rule of its own: a tag that resolves to an account
    ///     arrives judged as that account, so a private one refuses exactly like any other.
    /// </summary>
    [Fact]
    public void ABoardTagResolvingToAPrivateAccountIsNotAddable()
    {
        var verdict = RivalVisibilityPolicy.CanAdd(Candidate(Target));

        Assert.False(verdict.Allowed);
    }

    [Fact]
    public void ABlockOutranksEveryBasis()
    {
        var verdict = RivalVisibilityPolicy.CanAdd(
            Candidate(Target, isPublic: true, sharesCommunity: true, redeemedInvite: true, blocked: true));

        Assert.False(verdict.Allowed);
        Assert.Equal(RivalAddRefusal.Blocked, verdict.Refusal);
    }

    [Fact]
    public void YouCannotRivalYourself()
    {
        var verdict = RivalVisibilityPolicy.CanAdd(Candidate(Target, isPublic: true, isSelf: true));

        Assert.False(verdict.Allowed);
        Assert.Equal(RivalAddRefusal.Self, verdict.Refusal);
    }
}
