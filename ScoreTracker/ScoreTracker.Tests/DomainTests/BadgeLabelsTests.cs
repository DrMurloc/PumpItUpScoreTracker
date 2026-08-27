using System.Linq;
using ScoreTracker.Catalog.Contracts;
using ScoreTracker.Catalog.Domain;
using ScoreTracker.SharedKernel.Enums;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public sealed class BadgeLabelsTests
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
        Assert.Equal(expected, BadgeLabels.DisplayName(key));
    }

    [Fact]
    public void UnknownBadgesFallBackToTitleCaseSoNewVocabularyStaysReadable()
    {
        Assert.Equal("Quad Anchor Stomp", BadgeLabels.DisplayName("quad_anchor_stomp"));
    }

    /// <summary>
    ///     A hyphen is punctuation the term owns, not a separator: the real vocabulary is full
    ///     of cross-pad, co-op and 5-stair, and splitting those into two words invents terms
    ///     nobody uses.
    /// </summary>
    [Fact]
    public void AnUnknownBadgesHyphenSurvivesTheHumanizing()
    {
        Assert.Equal("Cross-pad Shuffle", BadgeLabels.DisplayName("cross-pad_shuffle"));
    }

    [Fact]
    public void EveryNamedBadgeBelongsToExactlyOneFamily()
    {
        // The owner's five families are meant to cover the whole vocabulary. A badge with a
        // display name but no family renders untinted, which reads as a bug rather than a gap.
        var orphans = BadgeLabels.KnownBadges
            .Where(b => BadgeLabels.CategoryFor(b) == null)
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
        Assert.Equal(expected, BadgeLabels.CategoryFor(badge));
    }

    [Fact]
    public void TheBadgesTheRollupThrewAwayHaveNamesToo()
    {
        // The rollup mapped these to nothing at all, so they could never be displayed. They
        // are ordinary badges here (docs/design/nuke-old-skill-categories.md §1).
        Assert.Equal("Doublesteps", BadgeLabels.DisplayName("doublestep"));
        Assert.Equal("Side-3 Singles", BadgeLabels.DisplayName("side3_singles"));
    }
}
