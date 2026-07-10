using System;
using System.Collections.Generic;
using System.Linq;
using ScoreTracker.PlayerProgress.Domain.Recap;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public sealed class RivalMatcherTests
{
    private static RivalMatcher.Candidate Candidate(double level, params Guid[] top50)
    {
        return new RivalMatcher.Candidate(Guid.NewGuid(), level, top50.ToHashSet());
    }

    [Fact]
    public void PoolLadderNeedsThreeCandidatesPerRung()
    {
        var community = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var country = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        var global = new[] { Guid.NewGuid() };

        Assert.Equal(country, RivalMatcher.SelectPool(community, country, global));
    }

    [Fact]
    public void CommunityPoolWinsWhenBigEnough()
    {
        var community = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        var country = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };

        Assert.Equal(community, RivalMatcher.SelectPool(community, country, Array.Empty<Guid>()));
    }

    [Fact]
    public void ThinLaddersFallThroughToTheGlobalPool()
    {
        var global = new[] { Guid.NewGuid() };

        Assert.Equal(global, RivalMatcher.SelectPool(Array.Empty<Guid>(), Array.Empty<Guid>(), global));
    }

    [Fact]
    public void RivalsOrderByTopFiftyOverlapThenLevelDistance()
    {
        var shared = Enumerable.Range(0, 50).Select(_ => Guid.NewGuid()).ToArray();
        var myTop50 = shared.ToHashSet();
        var bigOverlap = Candidate(21.0, shared.Take(30).ToArray());
        var closeLevel = Candidate(21.05, shared.Take(10).ToArray());
        var farLevel = Candidate(21.2, shared.Take(10).ToArray());

        var rivals = RivalMatcher.PickRivals(myTop50, 21.0, new[] { farLevel, closeLevel, bigOverlap });

        Assert.Equal(bigOverlap.UserId, rivals[0].Candidate.UserId);
        Assert.Equal(30, rivals[0].Overlap);
        Assert.Equal(closeLevel.UserId, rivals[1].Candidate.UserId);
        Assert.Equal(farLevel.UserId, rivals[2].Candidate.UserId);
    }

    [Fact]
    public void OnlyThreeRivalsAreChosen()
    {
        var pool = Enumerable.Range(0, 6).Select(_ => Candidate(21.0)).ToArray();

        Assert.Equal(3, RivalMatcher.PickRivals(new HashSet<Guid>(), 21.0, pool).Count);
    }
}
