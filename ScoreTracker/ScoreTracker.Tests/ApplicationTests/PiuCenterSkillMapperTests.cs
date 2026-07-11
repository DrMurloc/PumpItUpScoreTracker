using System;
using System.Linq;
using ScoreTracker.Catalog.Domain;
using ScoreTracker.SharedKernel.Enums;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class PiuCenterSkillMapperTests
{
    private static ChartSkillMetric Metric(string name, decimal value)
    {
        return new ChartSkillMetric(Guid.Empty, name, value, null);
    }

    [Fact]
    public void TopThreeSkillsMapToHighlightedTagsIncludingCompoundExpansion()
    {
        var record = PiuCenterSkillMapper.Map(Guid.Empty, new[]
        {
            Metric("top3:bracket_drill", 1),
            Metric("top3:bracket_run", 2),
            Metric("top3:mid6_doubles", 3)
        }, null, null);

        Assert.Equal(new[] { Skill.Brackets, Skill.BracketsAndRuns, Skill.Drills, Skill.HalfDouble },
            record.HighlightsSkill.OrderBy(s => s.ToString()).ToArray());
        Assert.Empty(record.ContainsSkills);
    }

    [Fact]
    public void BadgeFractionsOnlyTagPastTheThreshold()
    {
        var record = PiuCenterSkillMapper.Map(Guid.Empty, new[]
        {
            Metric("badge_fraction:twist_90", PiuCenterSkillMapper.BadgeFractionThreshold),
            Metric("badge_fraction:jack", PiuCenterSkillMapper.BadgeFractionThreshold - 0.01m)
        }, null, null);

        Assert.Contains(Skill.Twists, record.ContainsSkills);
        Assert.DoesNotContain(Skill.Jacks, record.ContainsSkills);
    }

    [Fact]
    public void FastAndSlowDeriveFromFolderNpsCutoffs()
    {
        var fast = PiuCenterSkillMapper.Map(Guid.Empty, new[] { Metric("nps", 12m) }, 10m, 4m);
        var slow = PiuCenterSkillMapper.Map(Guid.Empty, new[] { Metric("nps", 3m) }, 10m, 4m);
        var middle = PiuCenterSkillMapper.Map(Guid.Empty, new[] { Metric("nps", 7m) }, 10m, 4m);

        Assert.Contains(Skill.Fast, fast.ContainsSkills);
        Assert.Contains(Skill.Slow, slow.ContainsSkills);
        Assert.DoesNotContain(Skill.Fast, middle.ContainsSkills);
        Assert.DoesNotContain(Skill.Slow, middle.ContainsSkills);
    }

    [Fact]
    public void EndRunDerivesFromAFinalRunSegmentOnlyWhenTheChartIsNotRunDominant()
    {
        var closer = PiuCenterSkillMapper.Map(Guid.Empty, new[]
        {
            Metric("top3:twists", 1),
            Metric("last_segment_badge:run", 1)
        }, null, null);
        var runChart = PiuCenterSkillMapper.Map(Guid.Empty, new[]
        {
            Metric("top3:run", 1),
            Metric("last_segment_badge:run", 1)
        }, null, null);

        Assert.Contains(Skill.EndRun, closer.ContainsSkills);
        Assert.DoesNotContain(Skill.EndRun, runChart.ContainsSkills);
        Assert.Contains(Skill.Runs, runChart.HighlightsSkill);
    }

    [Fact]
    public void UnknownUpstreamSkillsAreIgnoredAndRetiredSkillsAreNeverEmitted()
    {
        var record = PiuCenterSkillMapper.Map(Guid.Empty, new[]
        {
            Metric("top3:some_future_skill", 1),
            Metric("badge_fraction:another_new_thing", 0.9m)
        }, null, null);

        Assert.Empty(record.ContainsSkills);
        Assert.Empty(record.HighlightsSkill);
    }

    [Theory]
    [InlineData("Slam_-_Novasonic_S7_ARCADE", "Slam", "Novasonic", "S7", "ARCADE")]
    [InlineData("1949_-_SLAM_D28_INFOBAR_TITLE_ARCADE", "1949", "SLAM", "D28", "ARCADE")]
    [InlineData("Wedding_Crashers_-_SHORT_CUT_-_-_SHK_S4_SHORTCUT", "Wedding_Crashers_-_SHORT_CUT_-", "SHK", "S4",
        "SHORTCUT")]
    [InlineData("Tribe_Attacker_-_Hi-G_D10_HALFDOUBLE_ARCADE", "Tribe_Attacker", "Hi-G", "D10",
        "HALFDOUBLE_ARCADE")]
    public void KeyParserHandlesInfobarTokensMultiSeparatorTitlesAndHalfDoubleSuffixes(string key,
        string song, string artist, string sordLevel, string variant)
    {
        Assert.True(PiuCenterKeyParser.TryParse(key, out var parts));
        Assert.Equal(song, parts.SongPart);
        Assert.Equal(artist, parts.ArtistPart);
        Assert.Equal(sordLevel, parts.SordLevel);
        Assert.Equal(variant, parts.Variant);
    }
}
