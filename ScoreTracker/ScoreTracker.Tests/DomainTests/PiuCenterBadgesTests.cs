using ScoreTracker.Catalog.Domain;
using ScoreTracker.SharedKernel.Enums;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public sealed class PiuCenterBadgesTests
{
    [Theory]
    [InlineData("staggered_bracket", "Staggered Brackets")]
    [InlineData("twist_over90", "Over-90 Twists")]
    [InlineData("anchor_run", "Anchor Runs")]
    [InlineData("5-stair", "5-Stairs")]
    [InlineData("yog_walk", "Yog Walks")]
    [InlineData("mid6_doubles", "Mid-6 Doubles")]
    [InlineData("co-op_pad_transition", "Co-op Pad Transitions")]
    public void KnownBadgesRenderTheirCuratedDisplayNames(string key, string expected)
    {
        Assert.Equal(expected, PiuCenterBadges.DisplayName(key));
    }

    [Fact]
    public void UnknownBadgesFallBackToTitleCaseSoNewVocabularyStaysReadable()
    {
        Assert.Equal("Quad Anchor Stomp", PiuCenterBadges.DisplayName("quad_anchor-stomp"));
    }

    [Theory]
    [InlineData("drill", SkillCategory.Stamina)]
    [InlineData("staggered_bracket", SkillCategory.Bracket)]
    [InlineData("twist_over90", SkillCategory.Twist)]
    [InlineData("hands", SkillCategory.Tech)]
    public void CategoryRidesTheBadgesRollupMapping(string key, SkillCategory expected)
    {
        Assert.Equal(expected, PiuCenterBadges.CategoryFor(key));
    }

    [Fact]
    public void DeliberatelyUnmappedBadgesGetNoColorFamily()
    {
        Assert.Null(PiuCenterBadges.CategoryFor("doublestep"));
    }
}
