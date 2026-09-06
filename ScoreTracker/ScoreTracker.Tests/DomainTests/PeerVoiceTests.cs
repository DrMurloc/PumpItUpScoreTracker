using System;
using System.Collections.Generic;
using ScoreTracker.Domain.Models;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

/// <summary>
///     A peer is an account or a board row, and everything that counts peers counts these. The
///     cases that matter are the ones where a set would otherwise merge or duplicate somebody.
/// </summary>
public sealed class PeerVoiceTests
{
    [Fact]
    public void AnAccountAndABoardRowAreNeverTheSamePeer()
    {
        var account = PeerVoice.Account(Guid.NewGuid());
        var board = PeerVoice.FromBoard(1, "URUSA#9487");

        Assert.NotEqual(account, board);
        Assert.False(account.IsFromBoard);
        Assert.True(board.IsFromBoard);
    }

    [Fact]
    public void TheSameAccountIsOnePeerHoweverOftenItIsNamed()
    {
        var id = Guid.NewGuid();

        Assert.Single(new HashSet<PeerVoice> { PeerVoice.Account(id), PeerVoice.Account(id) });
    }

    [Fact]
    public void TwoBoardRowsAreTwoPeers()
    {
        // Folding a person's several rows into one voice is the mirror's job and happens before
        // this type sees them; two DIFFERENT rows here really are two people.
        Assert.Equal(2, new HashSet<PeerVoice>
        {
            PeerVoice.FromBoard(1, "AZUL#1041"),
            PeerVoice.FromBoard(2, "URUSA#9487")
        }.Count);
    }

    [Fact]
    public void ABoardPeerIsNamedByItsTagAndAnAccountIsNot()
    {
        Assert.Equal("URUSA#9487", PeerVoice.FromBoard(3, "URUSA#9487").Tag);
        Assert.Null(PeerVoice.Account(Guid.NewGuid()).Tag);
    }
}
