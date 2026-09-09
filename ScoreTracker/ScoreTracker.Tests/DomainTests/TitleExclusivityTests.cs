using System.Linq;
using ScoreTracker.Domain.Models.Titles;
using ScoreTracker.Domain.Models.Titles.Phoenix2;
using ScoreTracker.SharedKernel.ValueTypes;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public sealed class TitleExclusivityTests
{
    private static Title Get(Name name) => Phoenix2TitleList.GetTitleByName(name);

    [Theory]
    [InlineData("[P.B] BRONZE", "[P.B] ALEXANDRITE")]
    [InlineData("[S] INTERMEDIATE LV.1", "SINGLE MASTER")]
    [InlineData("[D] ADVANCED LV.4", "[D] EXPERT LV.9")]
    public void TitlesOnTheSamePoolShareAnExclusivityGroup(string lower, string higher)
    {
        Assert.Equal(TitleExclusivity.GroupOf(Get(lower)), TitleExclusivity.GroupOf(Get(higher)));
    }

    [Theory]
    [InlineData("[P.B] GOLD", "[S] ADVANCED LV.1")]
    [InlineData("[S] EXPERT LV.1", "[D] EXPERT LV.1")]
    public void TheThreePoolsDoNotShareAGroup(string one, string other)
    {
        Assert.NotEqual(TitleExclusivity.GroupOf(Get(one)), TitleExclusivity.GroupOf(Get(other)));
    }

    [Fact]
    public void ATitleOffAPoolLadderCompetesWithNothing()
    {
        Assert.Null(TitleExclusivity.GroupOf(Get("[PHOENIX] SINGLE BOSS BREAKER")));
        Assert.Null(TitleExclusivity.GroupOf(Get("[TWIST S] EXPERT")));
        Assert.Null(TitleExclusivity.GroupOf(Get("BEGINNER")));
    }

    /// <summary>
    ///     The regression this whole type exists for. Title.Ladder rails a pool as bands —
    ///     [S] ADVANCED and [S] EXPERT are different rails — so grouping on it would leave a
    ///     band's top rung standing under the next band's bottom one.
    /// </summary>
    [Fact]
    public void BandsOfOnePoolAreOneGroupEvenThoughTheyAreDifferentRails()
    {
        var advanced = Get("[S] ADVANCED LV.10");
        var expert = Get("[S] EXPERT LV.1");

        Assert.NotEqual(advanced.Ladder, expert.Ladder);
        Assert.Equal(TitleExclusivity.GroupOf(advanced), TitleExclusivity.GroupOf(expert));

        var worn = TitleExclusivity.HighestOnly(new[] { advanced, expert }, t => t);
        Assert.Equal(new[] { expert }, worn);
    }

    [Fact]
    public void HighestOnlyKeepsOneRungPerPoolAndEverythingElse()
    {
        var held = new[]
        {
            Get("[P.B] BRONZE"), Get("[P.B] SILVER"), Get("[P.B] GOLD"),
            Get("[S] INTERMEDIATE LV.1"), Get("SINGLE MASTER"),
            Get("[PHOENIX] SINGLE BOSS BREAKER"), Get("[TWIST S] EXPERT")
        };

        var worn = TitleExclusivity.HighestOnly(held, t => t).Select(t => (string)t.Name).ToArray();

        Assert.Equal(new[]
        {
            "[P.B] GOLD", "SINGLE MASTER", "[PHOENIX] SINGLE BOSS BREAKER", "[TWIST S] EXPERT"
        }, worn);
    }

    [Fact]
    public void HighestOnlyLeavesASingleRungAlone()
    {
        var held = new[] { Get("[P.B] DIAMOND") };
        Assert.Equal(held, TitleExclusivity.HighestOnly(held, t => t));
    }

    [Fact]
    public void HighestOnlyProjectsThroughTheResolver()
    {
        var held = new[] { ("bronze-role", Get("[P.B] BRONZE")), ("gold-role", Get("[P.B] GOLD")) };

        var worn = TitleExclusivity.HighestOnly(held, pair => pair.Item2);

        Assert.Equal("gold-role", Assert.Single(worn).Item1);
    }

    /// <summary>
    ///     The rank has to be the number the title itself gates on, or a rung could move on the
    ///     ladder without moving in the ordering.
    /// </summary>
    [Fact]
    public void RankIsTheTitlesOwnThreshold()
    {
        Assert.Equal(15000, TitleExclusivity.RankIn(Get("[P.B] GOLD")));
        Assert.Equal(20000, TitleExclusivity.RankIn(Get("ABYSS ABSOLUTE")));
        Assert.Equal(0, TitleExclusivity.RankIn(Get("[TWIST S] EXPERT")));
    }

    /// <summary>Every pumbility rung is in a group, and nothing else in the list is.</summary>
    [Fact]
    public void OnlyPumbilityTitlesAreGrouped()
    {
        foreach (var title in Phoenix2TitleList.BuildList())
            Assert.Equal(title is Phoenix2PumbilityTitle, TitleExclusivity.GroupOf(title) != null);
    }
}
