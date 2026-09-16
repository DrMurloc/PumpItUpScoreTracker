using System;
using System.Linq;
using ScoreTracker.ChartIntelligence.Domain;
using ScoreTracker.SharedKernel.Enums;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

/// <summary>
///     The Hardmode cut (docs/design/hardmode-leaderboard.md §1). Every case here is a rule the
///     census cannot be allowed to get wrong quietly: a bad cut writes a plausible-looking list.
/// </summary>
public sealed class HardmodeCutTests
{
    [Theory]
    [InlineData(170, 0, 25)] // S21 — the flat 25, because a quarter of 170 is more
    [InlineData(107, 0, 25)] // S22
    [InlineData(66, 0, 16)] // S23 — a quarter, because it is fewer than 25
    [InlineData(27, 0, 6)] // S24
    [InlineData(13, 0, 3)] // S25
    public void TheCutIsTwentyFiveOrAQuarterWhicheverIsFewer(int folderSize, int unheld, int expected)
    {
        Assert.Equal(expected, HardmodeCut.CutSize(folderSize, unheld));
    }

    [Theory]
    [InlineData(2)] // S26 — 1948 and Paradoxx
    [InlineData(4)] // D28
    [InlineData(1)] // D29
    public void AFolderOfFourOrFewerGivesUpAtLeastOne(int folderSize)
    {
        // Without this a quarter of two is zero, and S26 left the list silently — which is to
        // say 1948 left the list silently.
        Assert.Equal(1, HardmodeCut.CutSize(folderSize, 0));
    }

    [Theory]
    [InlineData(4, 4, 4)] // D28: four charts, and on the measured data nobody holds some of them
    [InlineData(4, 2, 2)]
    [InlineData(4, 0, 1)] // all held - the floor still sends the rarest one
    [InlineData(2, 2, 2)] // S26, IF nobody held either. Paradoxx has 151 scorers, so it cannot.
    [InlineData(1, 1, 1)]
    public void ATinyFolderNobodyHoldsIsNotCappedAtOne(int folderSize, int unheld, int expected)
    {
        // The floor exists so a tiny folder is never dropped entirely; it was also capping one
        // that the unheld rule would have taken whole, so four unheld charts sent one and
        // silently dropped three while five unheld charts all qualified (D2 revised).
        Assert.Equal(expected, HardmodeCut.CutSize(folderSize, unheld));
    }

    [Fact]
    public void EveryUnheldChartQualifiesEvenWhenThereAreMoreThanTheCut()
    {
        // A low folder where nobody's fifty can reach: the cut opens to take all of them.
        Assert.Equal(83, HardmodeCut.CutSize(118, 83));
    }

    [Fact]
    public void TheCutNeverExceedsTheFolder()
    {
        // Defensive: an unheld count can only come from the same folder, but a cut wider than
        // the folder would silently take charts that are not in it.
        Assert.Equal(30, HardmodeCut.CutSize(30, 40));
    }

    [Fact]
    public void AnEmptyFolderGivesUpNothing()
    {
        Assert.Equal(0, HardmodeCut.CutSize(0, 0));
    }

    [Fact]
    public void SlotOneIsWorthFiftyAndSlotFiftyIsWorthOne()
    {
        Assert.Equal(50, HardmodeCut.SlotWeight(1));
        Assert.Equal(1, HardmodeCut.SlotWeight(50));
        Assert.Equal(26, HardmodeCut.SlotWeight(25));
    }

    [Fact]
    public void TheRarestChartComesFirst()
    {
        var ordered = HardmodeCut.InCutOrder(new[]
        {
            Candidate("Popular", points: 900, holders: 40),
            Candidate("Nobody", points: 0, holders: 0),
            Candidate("Middling", points: 300, holders: 12)
        });

        Assert.Equal(new[] { "Nobody", "Middling", "Popular" }, ordered.Select(c => c.Name));
    }

    [Fact]
    public void TiedChartsBreakOnScoringLevelDescendingThenName()
    {
        var ordered = HardmodeCut.InCutOrder(new[]
        {
            Candidate("Paradoxx", points: 0, holders: 0, scoringLevel: 25.8),
            Candidate("1948", points: 0, holders: 0, scoringLevel: 26.6),
            Candidate("Another", points: 0, holders: 0, scoringLevel: 26.6)
        });

        // Harder first, then alphabetical inside a scoring-level tie.
        Assert.Equal(new[] { "1948", "Another", "Paradoxx" }, ordered.Select(c => c.Name));
    }

    [Fact]
    public void AMissingScoringLevelFallsBackToTheChartLevelRatherThanSortingLast()
    {
        // 94 doubles charts and 19 singles at level 20+ carry no usable scoring level. Treating
        // that as zero would park them all at the end of every tie.
        var ordered = HardmodeCut.InCutOrder(new[]
        {
            Candidate("Rated low", points: 0, holders: 0, scoringLevel: 20.1, level: 26),
            Candidate("Unrated", points: 0, holders: 0, scoringLevel: null, level: 26)
        });

        Assert.Equal(new[] { "Unrated", "Rated low" }, ordered.Select(c => c.Name));
    }

    [Fact]
    public void QualifyingTakesTheCutFromTheOrderedFolder()
    {
        var folder = Enumerable.Range(0, 8)
            .Select(i => Candidate($"Chart {i:00}", points: i * 100, holders: i))
            .ToArray();

        // Eight charts: a quarter is two, and none are unheld except the first.
        var qualifying = HardmodeCut.Qualifying(folder);

        Assert.Equal(new[] { "Chart 00", "Chart 01" }, qualifying.Select(c => c.Name));
    }

    [Fact]
    public void QualifyingOpensTheCutWhenMostOfTheFolderIsUnheld()
    {
        var folder = Enumerable.Range(0, 8)
            .Select(i => Candidate($"Chart {i:00}", points: i < 5 ? 0 : i * 100, holders: i < 5 ? 0 : i))
            .ToArray();

        var qualifying = HardmodeCut.Qualifying(folder);

        Assert.Equal(5, qualifying.Count);
        Assert.All(qualifying, c => Assert.Equal(0, c.Holders));
    }

    [Theory]
    [InlineData(13, false)]
    [InlineData(10, false)]
    [InlineData(14, true)]
    [InlineData(26, true)]
    public void TheCensusOffersFoldersFromLevelFourteenUp(int level, bool offered)
    {
        // Below 14 the list recorded which charts nobody had played, not which were hard (D29).
        Assert.Equal(offered, HardmodeCut.IsOffered(level));
    }

    [Theory]
    [InlineData(170, 25)] // S21 — the flat 25
    [InlineData(66, 16)] // S23 — a quarter
    [InlineData(13, 3)] // S25
    [InlineData(4, 1)] // D28
    [InlineData(2, 0)] // S26 — no floor: nothing is priced against this end
    [InlineData(0, 0)]
    public void TheMostHeldEndIsTwentyFiveOrAQuarterWhicheverIsFewer(int folderSize, int expected)
    {
        Assert.Equal(expected, HardmodeCut.MostHeldSize(folderSize));
    }

    [Fact]
    public void TheMostHeldEndTakesTheMostHeldChartsFirst()
    {
        var folder = Enumerable.Range(0, 8)
            .Select(i => Candidate($"Chart {i:00}", points: (i + 1) * 100, holders: i + 1))
            .ToArray();

        // Eight charts: the Hardmode cut takes the two least held, the most-held end the two most.
        var mostHeld = HardmodeCut.MostHeld(folder);

        Assert.Equal(new[] { "Chart 07", "Chart 06" }, mostHeld.Select(c => c.Name));
    }

    [Fact]
    public void TheMostHeldEndNeverTakesAChartTheHardmodeCutTook()
    {
        // Five of eight nobody holds, so the Hardmode cut opens to all five; the most-held end
        // (two) must come from the three that are left, and never re-take a red one.
        var folder = Enumerable.Range(0, 8)
            .Select(i => Candidate($"Chart {i:00}", points: i < 5 ? 0 : i * 100, holders: i < 5 ? 0 : i))
            .ToArray();

        var hard = HardmodeCut.Qualifying(folder).Select(c => c.Name).ToHashSet();
        var mostHeld = HardmodeCut.MostHeld(folder);

        Assert.Equal(new[] { "Chart 07", "Chart 06" }, mostHeld.Select(c => c.Name));
        Assert.DoesNotContain(mostHeld, c => hard.Contains(c.Name));
    }

    [Fact]
    public void TheMostHeldEndNeverTakesAChartNobodyHolds()
    {
        // Seven of eight nobody holds: the Hardmode cut takes all seven, and one held chart is
        // left for an end sized two. An unheld chart is the opposite fact, so the end stays short.
        var folder = Enumerable.Range(0, 8)
            .Select(i => Candidate($"Chart {i:00}", points: i < 7 ? 0 : 500, holders: i < 7 ? 0 : 9))
            .ToArray();

        var mostHeld = HardmodeCut.MostHeld(folder);

        Assert.Equal("Chart 07", Assert.Single(mostHeld).Name);
    }

    [Fact]
    public void TiedMostHeldChartsBreakOnScoringLevelAscendingThenName()
    {
        var folder = new[]
        {
            Candidate("Rare 1", points: 1, holders: 1), Candidate("Rare 2", points: 2, holders: 1),
            Candidate("Rare 3", points: 3, holders: 1), Candidate("Rare 4", points: 4, holders: 1),
            Candidate("Rare 5", points: 5, holders: 1), Candidate("Rare 6", points: 6, holders: 1),
            Candidate("Hard staple", points: 900, holders: 30, scoringLevel: 21.8),
            Candidate("B staple", points: 900, holders: 30, scoringLevel: 20.2),
            Candidate("A staple", points: 900, holders: 30, scoringLevel: 20.2)
        };

        // Nine charts, so each end takes two: easier first, then alphabetical inside a tie.
        Assert.Equal(new[] { "A staple", "B staple" }, HardmodeCut.MostHeld(folder).Select(c => c.Name));
    }

    private static HardmodeCandidate Candidate(string name, double points, int holders,
        double? scoringLevel = 21.0, int level = 21)
    {
        return new HardmodeCandidate(Guid.NewGuid(), name, ChartType.Single, level, scoringLevel, points,
            holders);
    }
}
