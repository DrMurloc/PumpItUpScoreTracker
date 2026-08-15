using ScoreTracker.Domain.Models.Titles;
using ScoreTracker.SharedKernel.Enums;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

/// <summary>
///     The difficulty-titles fact drives Title Hunt's Phoenix 2 fallback, and it must track the
///     shipped lists rather than a hardcoded mix check: if Phoenix 2 ever gains difficulty
///     titles, the fallback has to lift on its own — and this pin has to fail so the fallback's
///     consumers get looked at.
/// </summary>
public sealed class TitleListsTests
{
    [Fact]
    public void PhoenixHasDifficultyTitlesToHunt()
    {
        Assert.True(TitleLists.HasDifficultyTitles(MixEnum.Phoenix));
    }

    [Fact]
    public void Phoenix2HasNoneWhichIsWhatTriggersTheTitleHuntFallback()
    {
        // P2's 272 titles are pumbility ladders, grade badges and play counts — none are
        // difficulty-typed. When this fails, the list gained one: revisit the fallback.
        Assert.False(TitleLists.HasDifficultyTitles(MixEnum.Phoenix2));
    }

    [Fact]
    public void LegacyMixesHaveNoTitleTaxonomyAtAll()
    {
        Assert.False(TitleLists.HasDifficultyTitles(MixEnum.XX));
    }
}
