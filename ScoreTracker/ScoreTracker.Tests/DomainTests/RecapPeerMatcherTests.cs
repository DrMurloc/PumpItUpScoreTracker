using System;
using System.Collections.Generic;
using System.Linq;
using ScoreTracker.PlayerProgress.Domain.Recap;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public sealed class RecapPeerMatcherTests
{
    private static RecapPeerMatcher.Candidate Candidate(double level, params Guid[] top50)
    {
        return new RecapPeerMatcher.Candidate(Guid.NewGuid(), level, top50.ToHashSet());
    }

    [Fact]
    public void PeersOrderByTopFiftyOverlapThenLevelDistance()
    {
        var shared = Enumerable.Range(0, 50).Select(_ => Guid.NewGuid()).ToArray();
        var myTop50 = shared.ToHashSet();
        var bigOverlap = Candidate(21.0, shared.Take(30).ToArray());
        var closeLevel = Candidate(21.05, shared.Take(10).ToArray());
        var farLevel = Candidate(21.2, shared.Take(10).ToArray());

        var peers = RecapPeerMatcher.PickPeers(myTop50, 21.0, new[] { farLevel, closeLevel, bigOverlap });

        Assert.Equal(bigOverlap.UserId, peers[0].Candidate.UserId);
        Assert.Equal(30, peers[0].Overlap);
        Assert.Equal(closeLevel.UserId, peers[1].Candidate.UserId);
        Assert.Equal(farLevel.UserId, peers[2].Candidate.UserId);
    }

    [Fact]
    public void PickCountIsRespected()
    {
        var pool = Enumerable.Range(0, 6).Select(_ => Candidate(21.0)).ToArray();

        Assert.Equal(3, RecapPeerMatcher.PickPeers(new HashSet<Guid>(), 21.0, pool).Count);
        Assert.Equal(2, RecapPeerMatcher.PickPeers(new HashSet<Guid>(), 21.0, pool, 2).Count);
    }
}
