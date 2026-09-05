using System;
using System.Collections.Generic;
using System.Linq;
using ScoreTracker.Domain.Models;
using ScoreTracker.Rivals.Domain;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public sealed class PeerStandingCalculatorTests
{
    private static readonly DateTimeOffset Sealed = new(2026, 8, 31, 0, 0, 0, TimeSpan.Zero);

    private static PeerStandingCalculator.PeerPass Pass(Guid key, int score, bool board = false) =>
        new(key, score, board);

    private static PeerStandingCalculator.SourceMembers Source(PeerSourceKind kind, params Guid[] members) =>
        new(kind, null, null, false, false, members.ToHashSet());

    private static IReadOnlySet<Guid> Union(params PeerStandingCalculator.SourceMembers[] sources) =>
        sources.SelectMany(s => s.Members).ToHashSet();

    [Fact]
    public void RanksOnlyPassesInsideTheUnionAndReadsThePlaceWithYouInTheCohort()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var c = Guid.NewGuid();
        var outsider = Guid.NewGuid();
        var rivals = Source(PeerSourceKind.Rivals, a, b, c);

        var standing = PeerStandingCalculator.Compute(950_000,
            new[] { Pass(a, 990_000), Pass(b, 940_000), Pass(outsider, 999_000) },
            new HashSet<Guid>(), new[] { rivals }, Union(rivals), null);

        // Two rivals passed it, one above you: #2 of 3. The outsider's 999k never counts, and
        // the rival who has not passed it is the not-passed count.
        Assert.Equal(2, standing.Place);
        Assert.Equal(3, standing.Cohort);
        Assert.Equal(1, standing.NotPassed);
        Assert.Equal(2 / 3.0, standing.Percentile!.Value, 10);
    }

    [Fact]
    public void ATiedScoreIsNotAboveYou()
    {
        var a = Guid.NewGuid();
        var rivals = Source(PeerSourceKind.Rivals, a);

        var standing = PeerStandingCalculator.Compute(950_000, new[] { Pass(a, 950_000) },
            new HashSet<Guid>(), new[] { rivals }, Union(rivals), null);

        Assert.True(standing.IsFirst);
        Assert.Equal(1.0, standing.Percentile);
    }

    [Fact]
    public void ABrokenAttemptCountsAsNotPassedAndNeverAsAScore()
    {
        var passer = Guid.NewGuid();
        var breaker = Guid.NewGuid();
        var rivals = Source(PeerSourceKind.Rivals, passer, breaker);

        var standing = PeerStandingCalculator.Compute(900_000, new[] { Pass(passer, 910_000) },
            new HashSet<Guid> { breaker }, new[] { rivals }, Union(rivals), null);

        Assert.Equal(1, standing.Passed);
        Assert.Equal(1, standing.Broke);
        Assert.Equal(1, standing.NotPassed);
        Assert.Equal(2, standing.Place);
    }

    [Fact]
    public void APlayerWithAPassAndAStaleBrokenRowIsAPasser()
    {
        var player = Guid.NewGuid();
        var rivals = Source(PeerSourceKind.Rivals, player);

        var standing = PeerStandingCalculator.Compute(900_000, new[] { Pass(player, 910_000) },
            new HashSet<Guid> { player }, new[] { rivals }, Union(rivals), null);

        Assert.Equal(0, standing.Broke);
        Assert.Equal(1, standing.Passed);
    }

    [Fact]
    public void APlayerInTwoSourcesCountsOnceInTheUnionAndOnEachLine()
    {
        var both = Guid.NewGuid();
        var rivalOnly = Guid.NewGuid();
        var rivals = Source(PeerSourceKind.Rivals, both, rivalOnly);
        var club = Source(PeerSourceKind.Community, both);

        var standing = PeerStandingCalculator.Compute(900_000,
            new[] { Pass(both, 990_000), Pass(rivalOnly, 880_000) },
            new HashSet<Guid>(), new[] { rivals, club }, Union(rivals, club), null);

        Assert.Equal(2, standing.PeerCount);
        Assert.Equal(2, standing.Place);
        var rivalLine = standing.Sources.Single(s => s.Kind == PeerSourceKind.Rivals);
        var clubLine = standing.Sources.Single(s => s.Kind == PeerSourceKind.Community);
        Assert.Equal((2, 3), (rivalLine.Place, rivalLine.Of));
        Assert.Equal((2, 2), (clubLine.Place, clubLine.Of));
    }

    [Fact]
    public void PerfectGamesAreCountedAmongThePasses()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var rivals = Source(PeerSourceKind.Rivals, a, b);

        var standing = PeerStandingCalculator.Compute(PeerStandingCalculator.PerfectGame,
            new[] { Pass(a, PeerStandingCalculator.PerfectGame), Pass(b, 999_000) },
            new HashSet<Guid>(), new[] { rivals }, Union(rivals), null);

        Assert.Equal(1, standing.PerfectGames);
        Assert.True(standing.IsFirst);
    }

    [Fact]
    public void ABoardPlacementCarriesTheMirrorsInstantOnlyWhenOneWasCounted()
    {
        var ghostEdge = Guid.NewGuid();
        var site = Guid.NewGuid();
        var rivals = Source(PeerSourceKind.Rivals, ghostEdge, site);

        var withGhost = PeerStandingCalculator.Compute(900_000,
            new[] { Pass(ghostEdge, 995_000, board: true), Pass(site, 950_000) },
            new HashSet<Guid>(), new[] { rivals }, Union(rivals), Sealed);
        var siteOnly = PeerStandingCalculator.Compute(900_000, new[] { Pass(site, 950_000) },
            new HashSet<Guid>(), new[] { rivals }, Union(rivals), Sealed);

        Assert.Equal(Sealed, withGhost.OfficialAsOf);
        Assert.Equal(1, withGhost.Sources.Single().FromOfficialBoard);
        Assert.Null(siteOnly.OfficialAsOf);
    }

    [Fact]
    public void ADuplicateKeyKeepsItsHigherScoreAndCountsOnce()
    {
        var player = Guid.NewGuid();
        var rivals = Source(PeerSourceKind.Rivals, player);

        var standing = PeerStandingCalculator.Compute(900_000,
            new[] { Pass(player, 910_000), Pass(player, 880_000) },
            new HashSet<Guid>(), new[] { rivals }, Union(rivals), null);

        Assert.Equal(1, standing.Passed);
        Assert.Equal(2, standing.Place);
    }

    [Fact]
    public void NoPassesMeansNoCohortButTheLinesStillSayWhoWasAsked()
    {
        var a = Guid.NewGuid();
        var rivals = Source(PeerSourceKind.Rivals, a);

        var standing = PeerStandingCalculator.Compute(900_000, Array.Empty<PeerStandingCalculator.PeerPass>(),
            new HashSet<Guid> { a }, new[] { rivals }, Union(rivals), null);

        Assert.False(standing.HasCohort);
        Assert.Equal(1, standing.Broke);
        Assert.Equal(1, standing.Sources.Single().NotPassed);
    }
}
