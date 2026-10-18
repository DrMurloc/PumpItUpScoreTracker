using System.Linq;
using ScoreTracker.Domain.Models.Titles.Phoenix;
using ScoreTracker.Domain.Models.Titles.Phoenix2;
using ScoreTracker.SharedKernel.Enums;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

/// <summary>
///     What a chart title asks of its chart, read from the title rather than parsed out of its
///     sentence — the titles page prints it on the chart's card (docs/design/titles-overhaul.md,
///     chart titles). Pinned against the shipped lists, because a constructor argument silently
///     dropped on the way to the property is exactly the failure this would otherwise hide.
/// </summary>
public sealed class ChartTitleRequirementTests
{
    [Fact]
    public void EveryPhoenix2SkillRungAsksForSssAndTheTwoChartBadgesForSssPlus()
    {
        var titles = Phoenix2TitleList.BuildList().OfType<Phoenix2ChartGradeTitle>().ToArray();

        Assert.Equal(92, titles.Length);
        Assert.Equal(90, titles.Count(t => t.MinimumGrade == PhoenixLetterGrade.SSS));
        Assert.Equal(2, titles.Count(t => t.MinimumGrade == PhoenixLetterGrade.SSSPlus));
    }

    [Fact]
    public void ABossBreakerAsksForNoGradeInEitherMix()
    {
        Assert.All(Phoenix2TitleList.BuildList().OfType<Phoenix2ChartClearTitle>(),
            t => Assert.Null(t.MinimumGrade));
        Assert.All(PhoenixTitleList.BuildList().OfType<PhoenixBossBreakerTitle>(),
            t => Assert.Null(t.MinimumGrade));
    }

    [Fact]
    public void APhoenixSkillRungAsksForTheGradeItsSentenceNames()
    {
        var skills = PhoenixTitleList.BuildList().OfType<PhoenixSkillTitle>().ToArray();

        Assert.Equal(60, skills.Length);
        Assert.All(skills, t =>
        {
            Assert.Equal(PhoenixLetterGrade.SSS, t.MinimumGrade);
            Assert.StartsWith("Get SSS on ", t.Description);
        });
    }
}
