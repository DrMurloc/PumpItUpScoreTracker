using System;
using System.Collections.Generic;
using System.Linq;
using ScoreTracker.Domain.Models.Titles;
using ScoreTracker.Domain.Models.Titles.Phoenix;
using ScoreTracker.Domain.Models.Titles.Phoenix2;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

/// <summary>
///     The display rails every title is drawn on. These are ratchets over data that is easy to
///     mistype and impossible to eyeball across 485 declarations: a rail with a hole in it, a
///     duplicated rung, or a title that quietly fell off its ladder all fail here rather than
///     rendering as a gap on the page.
/// </summary>
public sealed class TitleRailTests
{
    private static IEnumerable<Title> Phoenix => PhoenixTitleList.BuildList();
    private static IEnumerable<Title> Phoenix2 => Phoenix2TitleList.BuildList();

    private static IDictionary<Name, Title[]> RailsOf(IEnumerable<Title> titles)
    {
        return titles.Where(t => t.Ladder != null)
            .GroupBy(t => t.Ladder!.Value)
            .ToDictionary(g => g.Key, g => g.ToArray());
    }

    public static TheoryData<string> BothMixes => new() { "Phoenix", "Phoenix 2" };

    private static IEnumerable<Title> For(string mix)
    {
        return mix == "Phoenix" ? Phoenix : Phoenix2;
    }

    [Theory]
    [MemberData(nameof(BothMixes))]
    public void EveryRailNumbersItsRungsFromOneWithoutGapsOrDuplicates(string mix)
    {
        foreach (var (rail, titles) in RailsOf(For(mix)))
        {
            var rungs = titles.Select(t => t.Rung).OrderBy(r => r).ToArray();
            Assert.Equal(Enumerable.Range(1, titles.Length), rungs);
        }
    }

    [Theory]
    [MemberData(nameof(BothMixes))]
    public void ATitleIsEitherOnARailWithARungOrOnNeither(string mix)
    {
        foreach (var title in For(mix))
            Assert.Equal(title.Ladder != null, title.Rung > 0);
    }

    [Fact]
    public void TheOnlyComputedTitlesOffARailAreTheOnesWithNothingToClimb()
    {
        // A one-off is normally a site-detected badge with no formula at all, which is the
        // whole of Phoenix's twenty. Phoenix 2 has three we do compute and still cannot rail:
        // SPECIALIST spans every skill track rather than sitting on one, and two chart-grade
        // badges are lone charts. Anything else appearing here is a title that fell off a rail.
        Assert.All(Phoenix.Where(t => t.Ladder == null), t => Assert.Equal(0, t.CompletionRequired));

        var computedOneOffs = Phoenix2.Where(t => t.Ladder == null && t.CompletionRequired > 0)
            .Select(t => t.Name.ToString()).OrderBy(n => n).ToArray();
        Assert.Equal(new[] { "NO SKILLS NO PUMP", "PUMP IS A SENSE", "SPECIALIST" }, computedOneOffs);
    }

    [Fact]
    public void PhoenixDrawsFortySevenRailsOverAllButTwentyOfItsTitles()
    {
        var rails = RailsOf(Phoenix);
        Assert.Equal(213, Phoenix.Count());
        Assert.Equal(47, rails.Count);
        Assert.Equal(20, Phoenix.Count(t => t.Ladder == null));
    }

    [Fact]
    public void PhoenixTwoDrawsFortyEightRailsOverAllButElevenOfItsTitles()
    {
        var rails = RailsOf(Phoenix2);
        Assert.Equal(272, Phoenix2.Count());
        Assert.Equal(48, rails.Count);
        Assert.Equal(11, Phoenix2.Count(t => t.Ladder == null));
    }

    [Fact]
    public void PhoenixFolderTiersAreFourRailsNotNineScoringLadders()
    {
        var rails = RailsOf(Phoenix);
        Assert.Equal(10, rails[(Name)"INTERMEDIATE"].Length);
        Assert.Equal(10, rails[(Name)"ADVANCED"].Length);
        Assert.Equal(10, rails[(Name)"EXPERT"].Length);
        Assert.Single(rails[(Name)"THE MASTER"]);
    }

    [Fact]
    public void EachPhoenixSkillTrackCarriesTenRungsAndItsExpertCapstone()
    {
        var rails = RailsOf(Phoenix);
        foreach (var skill in new Name[] { "BRACKET", "HALF", "GIMMICK", "DRILL", "RUN", "TWIST" })
        {
            var rail = rails[skill].OrderBy(t => t.Rung).ToArray();
            Assert.Equal(11, rail.Length);
            // The ten rungs compute; the capstone is a basic title the official site awards.
            Assert.All(rail.Take(10), t => Assert.IsType<PhoenixSkillTitle>(t));
            Assert.IsType<PhoenixBasicTitle>(rail[10]);
            Assert.Equal((Name)$"[{skill}] EXPERT", rail[10].Name);
        }
    }

    [Fact]
    public void EachPhoenixTwoSkillTrackCarriesTenRungsAndItsExpertCapstone()
    {
        var rails = RailsOf(Phoenix2);
        foreach (var skill in new Name[]
                     { "TWIST S", "TWIST D", "RUN S", "RUN D", "DRILL", "GIMMICK", "SLOW", "HALF", "BRACKET" })
        {
            var rail = rails[skill].OrderBy(t => t.Rung).ToArray();
            Assert.Equal(11, rail.Length);
            Assert.All(rail.Take(10), t => Assert.IsType<Phoenix2ChartGradeTitle>(t));
            // Phoenix 2 computes its capstones, where Phoenix leaves them to the import.
            Assert.IsType<Phoenix2TitleSetTitle>(rail[10]);
        }
    }

    [Fact]
    public void PhoenixTwoPoolsAreThreeRailsPrefixedTheWayTheirTitlesRead()
    {
        var rails = RailsOf(Phoenix2);
        Assert.Equal(31, rails[(Name)"[S]"].Length);
        Assert.Equal(31, rails[(Name)"[D]"].Length);
        Assert.Equal(8, rails[(Name)"[P.B]"].Length);
    }

    [Fact]
    public void ABossBreakerRailIsOneMixWithItsSingleAheadOfItsDouble()
    {
        var phoenix = Phoenix.OfType<PhoenixBossBreakerTitle>()
            .Select(t => (t.Ladder, t.Rung, t.Type)).ToArray();
        var phoenix2 = Phoenix2.OfType<Phoenix2ChartClearTitle>()
            .Select(t => (t.Ladder, t.Rung, t.Type)).ToArray();

        foreach (var rails in new[] { phoenix, phoenix2 })
        foreach (var rail in rails.GroupBy(t => t.Ladder))
        {
            Assert.InRange(rail.Count(), 1, 2);
            Assert.All(rail, t => Assert.Equal(t.Type == ChartType.Single ? 1 : rail.Count(), t.Rung));
        }

        Assert.Equal(20, phoenix.Select(t => t.Ladder).Distinct().Count());
        Assert.Equal(21, phoenix2.Select(t => t.Ladder).Distinct().Count());

        // EXTRA is the trap: a mix whose only boss title is a double, so "single is rung 1"
        // has to mean "first of what exists" rather than a fixed slot.
        var extra = phoenix.Single(t => t.Ladder == (Name)"EXTRA");
        Assert.Equal(ChartType.Double, extra.Type);
        Assert.Equal(1, extra.Rung);
    }

    [Fact]
    public void PhoenixPlateFamiliesClimbBronzeToPlatinum()
    {
        var rails = RailsOf(Phoenix.Where(t => t.Category == (Name)"Plates"));
        Assert.Equal(8, rails.Count);
        foreach (var rail in rails.Values)
        {
            var byRung = rail.OrderBy(t => t.Rung).Select(t => t.Name.ToString()).ToArray();
            Assert.Equal(4, byRung.Length);
            Assert.Contains("Bronze", byRung[0]);
            Assert.Contains("Silver", byRung[1]);
            Assert.Contains("Gold", byRung[2]);
            Assert.Contains("Platinum", byRung[3]);
        }
    }

    [Fact]
    public void RungOrderIsNotRequirementOrder()
    {
        // The reason Rung exists at all. Advanced asks for more rating at Lv.3 (39,000, on
        // the 20s) than at Lv.4 (15,000, on the 21s), so sorting a rail by requirement
        // scrambles it — and Expert Lv.1 and Lv.6 are both exactly 40,000, so requirement
        // cannot even break the tie. Every skill and boss-breaker title requires 1.
        var advanced = RailsOf(Phoenix)[(Name)"ADVANCED"];
        var byRung = advanced.OrderBy(t => t.Rung).Select(t => t.Name.ToString()).ToArray();
        var byRequirement = advanced.OrderBy(t => t.CompletionRequired).Select(t => t.Name.ToString()).ToArray();
        Assert.NotEqual(byRung, byRequirement);

        var expert = RailsOf(Phoenix)[(Name)"EXPERT"];
        Assert.Equal(2, expert.Count(t => t.CompletionRequired == 40000));

        var bracket = RailsOf(Phoenix2)[(Name)"BRACKET"].Where(t => t.Rung <= 10).ToArray();
        Assert.All(bracket, t => Assert.Equal(1, t.CompletionRequired));
    }
}
