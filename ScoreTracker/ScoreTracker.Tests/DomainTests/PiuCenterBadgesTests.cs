using System.Linq;
using ScoreTracker.Catalog.Contracts;
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

    [Fact]
    public void EveryNamedBadgeBelongsToExactlyOneFamily()
    {
        // The owner's five families are meant to cover the whole vocabulary. A badge with a
        // display name but no family renders untinted, which reads as a bug rather than a gap.
        var orphans = PiuCenterBadges.KnownBadges
            .Where(b => PiuCenterBadges.CategoryFor(b) == null)
            .ToArray();

        Assert.Empty(orphans);
    }

    [Theory]
    // The owner's calls that a reader would not guess: jacks and jumps are Tech rather than
    // stamina, side-3 singles are a Twists problem, and the far-pad vocabulary is its own family.
    [InlineData("jack", BadgeCategory.Tech)]
    [InlineData("jump", BadgeCategory.Tech)]
    [InlineData("side3_singles", BadgeCategory.Twists)]
    [InlineData("10-stair", BadgeCategory.DoublesTech)]
    [InlineData("5-stair", BadgeCategory.Tech)]
    [InlineData("drill", BadgeCategory.StaminaAndRuns)]
    [InlineData("bracket_twist", BadgeCategory.Brackets)]
    public void TheOwnerSpecifiedFamiliesAreWhatTheySaidTheyAre(string badge, BadgeCategory expected)
    {
        Assert.Equal(expected, PiuCenterBadges.CategoryFor(badge));
    }

    [Fact]
    public void TheBadgesTheRollupThrewAwayHaveNamesToo()
    {
        // The rollup mapped these to nothing at all, so they could never be displayed. They
        // are ordinary badges here (docs/design/nuke-old-skill-categories.md §1).
        Assert.Equal("Doublesteps", PiuCenterBadges.DisplayName("doublestep"));
        Assert.Equal("Side-3 Singles", PiuCenterBadges.DisplayName("side3_singles"));
    }
}
