using System;
using System.Collections.Generic;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public sealed class PlayerAudienceTests
{
    private static readonly Guid Viewer = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Rival = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Member = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid Stranger = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private static PlayerAudience Audience() => new(Viewer,
        new Dictionary<Guid, IReadOnlyList<Name>> { [Member] = new[] { Name.From("Seoul Pump") } },
        new HashSet<Guid> { Rival });

    [Fact]
    public void YouAlwaysSeeYourself()
    {
        var visibility = Audience().Describe(Viewer, targetIsPublic: false);
        Assert.True(visibility.CanView);
        Assert.True(visibility.IsYou);
    }

    [Fact]
    public void APublicStrangerIsVisibleOnNoOtherBasis()
    {
        var visibility = Audience().Describe(Stranger, targetIsPublic: true);
        Assert.True(visibility.CanView);
        Assert.False(visibility.IsYou);
        Assert.False(visibility.IsYourRival);
        Assert.Empty(visibility.SharedCommunities);
    }

    [Fact]
    public void APrivateStrangerIsHidden()
    {
        Assert.False(Audience().Describe(Stranger, targetIsPublic: false).CanView);
    }

    [Fact]
    public void APrivateRivalIsVisibleBecauseOfTheEdge()
    {
        var visibility = Audience().Describe(Rival, targetIsPublic: false);
        Assert.True(visibility.CanView);
        Assert.True(visibility.IsYourRival);
    }

    [Fact]
    public void APrivateCommunityMemberIsVisibleAndTheCommunityIsNamed()
    {
        var visibility = Audience().Describe(Member, targetIsPublic: false);
        Assert.True(visibility.CanView);
        Assert.Equal(new[] { Name.From("Seoul Pump") }, visibility.SharedCommunities);
    }

    [Fact]
    public void TheVisibleSetIsYouPlusMembersPlusRivalsAndNeverThePublicPredicate()
    {
        var visible = Audience().VisibleUserIds;
        Assert.Equal(new HashSet<Guid> { Viewer, Member, Rival }, visible);
    }

    [Fact]
    public void AnonymousSeesPublicPlayersOnly()
    {
        var anonymous = PlayerAudience.Anonymous;
        Assert.Empty(anonymous.VisibleUserIds);
        Assert.True(anonymous.Describe(Stranger, targetIsPublic: true).CanView);
        Assert.False(anonymous.Describe(Stranger, targetIsPublic: false).CanView);
        Assert.False(anonymous.Describe(Stranger, targetIsPublic: true).IsYou);
    }
}
