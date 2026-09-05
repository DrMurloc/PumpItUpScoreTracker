using System;
using System.Collections.Generic;
using ScoreTracker.Domain.Models;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public sealed class PeerSourceSelectionTests
{
    private static readonly Guid Club = Guid.NewGuid();
    private static readonly Guid Region = Guid.NewGuid();

    [Fact]
    public void ANeverSavedSettingIsTheCompetitiveBandAlone()
    {
        var selection = PeerSourceSelection.Parse(null);

        Assert.True(selection.CompetitiveLevel);
        Assert.False(selection.Rivals);
        Assert.False(selection.Pumbility);
        Assert.Empty(selection.CommunityIds);
    }

    [Fact]
    public void RoundTripsEverySource()
    {
        var original = new PeerSourceSelection(true, true, true, new HashSet<Guid> { Club, Region });

        var parsed = PeerSourceSelection.Parse(original.Serialize());

        Assert.True(parsed.Rivals);
        Assert.True(parsed.CompetitiveLevel);
        Assert.True(parsed.Pumbility);
        Assert.Equal(new HashSet<Guid> { Club, Region }, parsed.CommunityIds);
    }

    [Fact]
    public void EverythingUntickedIsARealChoiceNotTheDefault()
    {
        // The version token alone means "I chose nothing" — the default only stands in for a
        // player who never opened the dialog.
        var parsed = PeerSourceSelection.Parse(PeerSourceSelection.Nothing.Serialize());

        Assert.False(parsed.Any);
        Assert.False(parsed.CompetitiveLevel);
    }

    [Fact]
    public void AValueWithoutTheVersionTokenIsNotOursAndReadsAsTheDefault()
    {
        var parsed = PeerSourceSelection.Parse("Rivals,Pumbility");

        Assert.True(parsed.CompetitiveLevel);
        Assert.False(parsed.Rivals);
    }

    [Fact]
    public void UnknownTokensAreIgnoredSoARolledBackReleaseCanReadANewerSave()
    {
        var parsed = PeerSourceSelection.Parse("v1,Rivals,Guilds:abc,Community:not-a-guid");

        Assert.True(parsed.Rivals);
        Assert.Empty(parsed.CommunityIds);
    }

    [Fact]
    public void SerializesCommunitiesInAStableOrder()
    {
        var a = new PeerSourceSelection(false, false, false, new HashSet<Guid> { Club, Region }).Serialize();
        var b = new PeerSourceSelection(false, false, false, new HashSet<Guid> { Region, Club }).Serialize();

        Assert.Equal(a, b);
    }
}
