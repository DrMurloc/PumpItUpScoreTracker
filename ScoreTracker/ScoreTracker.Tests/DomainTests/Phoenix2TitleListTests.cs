using System;
using System.Collections.Generic;
using System.Linq;
using ScoreTracker.Domain.Models.Titles.Phoenix2;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public sealed class Phoenix2TitleListTests
{
    private static readonly DateTimeOffset When = new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);

    private static RecordedPhoenixScore Attempt(Guid chartId, int score, bool isBroken = false,
        PhoenixPlate plate = PhoenixPlate.SuperbGame)
    {
        return new RecordedPhoenixScore(chartId, score, plate, isBroken, When);
    }

    [Fact]
    public void CatalogCarriesAllTitlesFromTheOfficialPageWithUniqueNames()
    {
        var titles = Phoenix2TitleList.BuildList().ToArray();

        Assert.Equal(272, titles.Length);
        Assert.Equal(titles.Length, titles.Select(t => t.Name).Distinct().Count());
        Assert.All(titles, t => Assert.Same(t, Phoenix2TitleList.GetTitleByName(t.Name)));
    }

    [Fact]
    public void SinglesLadderProgressIsTheSinglesPoolValue()
    {
        // Two L24 SSS+ SG singles, priced one level up: Base(25)=260 x 1.508 = 392.08 each ->
        // singles pool 784. The doubles chart (250 x 1.508 = 377) must not leak into the
        // singles ladder.
        var s1 = new ChartBuilder().WithType(ChartType.Single).WithLevel(24).Build();
        var s2 = new ChartBuilder().WithType(ChartType.Single).WithLevel(24).Build();
        var d1 = new ChartBuilder().WithType(ChartType.Double).WithLevel(24).Build();
        var charts = new[] { s1, s2, d1 }.ToDictionary(c => c.Id);

        var progress = Phoenix2TitleList.BuildProgress(charts, new[]
        {
            Attempt(s1.Id, 995000), Attempt(s2.Id, 995000), Attempt(d1.Id, 995000)
        }, new HashSet<Name>());

        var singlesLv1 = progress.Single(p => p.Title.Name == "[S] INTERMEDIATE LV.1");
        var doublesLv1 = progress.Single(p => p.Title.Name == "[D] INTERMEDIATE LV.1");
        var totalTier = progress.Single(p => p.Title.Name == "[P.B] BRONZE");
        Assert.Equal(784, singlesLv1.CompletionCount);
        Assert.Equal(377, doublesLv1.CompletionCount);
        Assert.Equal(784 + 377, totalTier.CompletionCount);
        Assert.False(singlesLv1.IsComplete);
    }

    [Fact]
    public void SinglesLadderCompletesAtItsThreshold()
    {
        // Fifteen L24 SSS+ SG singles = 15 x 392.08 = 5881 >= the LV.1 threshold of 5000
        // (and still short of LV.2's 6000).
        var charts = Enumerable.Range(0, 15)
            .Select(_ => new ChartBuilder().WithType(ChartType.Single).WithLevel(24).Build())
            .ToDictionary(c => c.Id);

        var progress = Phoenix2TitleList.BuildProgress(charts,
            charts.Keys.Select(id => Attempt(id, 995000)).ToArray(), new HashSet<Name>());

        Assert.True(progress.Single(p => p.Title.Name == "[S] INTERMEDIATE LV.1").IsComplete);
        Assert.False(progress.Single(p => p.Title.Name == "[S] INTERMEDIATE LV.2").IsComplete);
    }

    [Fact]
    public void PoolsCapAtTheirTopFifty()
    {
        // 55 identical singles: only 50 count -> 50 x 392.08 = 19604, not 55 x 392.08.
        var charts = Enumerable.Range(0, 55)
            .Select(_ => new ChartBuilder().WithType(ChartType.Single).WithLevel(24).Build())
            .ToDictionary(c => c.Id);

        var progress = Phoenix2TitleList.BuildProgress(charts,
            charts.Keys.Select(id => Attempt(id, 995000)).ToArray(), new HashSet<Name>());

        Assert.Equal(19604, progress.Single(p => p.Title.Name == "[S] INTERMEDIATE LV.1").CompletionCount);
    }

    [Theory]
    [InlineData(990000, false, true)] // SSS meets the bar
    [InlineData(985000, false, false)] // SS+ does not
    [InlineData(990000, true, false)] // broken never counts
    public void SkillTitlesRequireTheGradeOnTheExactChart(int score, bool isBroken, bool expectComplete)
    {
        // [TWIST S] LV.1 = Scorpion King S15, SSS or more.
        var chart = new ChartBuilder().WithSongName("Scorpion King").WithType(ChartType.Single)
            .WithLevel(15).Build();
        var charts = new Dictionary<Guid, Chart> { [chart.Id] = chart };

        var progress = Phoenix2TitleList.BuildProgress(charts,
            new[] { Attempt(chart.Id, score, isBroken) }, new HashSet<Name>());

        Assert.Equal(expectComplete, progress.Single(p => p.Title.Name == "[TWIST S] LV.1").IsComplete);
    }

    [Fact]
    public void BossBreakersCompleteOnAnyUnbrokenPass()
    {
        var chart = new ChartBuilder().WithSongName("1948").WithType(ChartType.Single).WithLevel(26).Build();
        var charts = new Dictionary<Guid, Chart> { [chart.Id] = chart };

        var passed = Phoenix2TitleList.BuildProgress(charts,
            new[] { Attempt(chart.Id, 820000) }, new HashSet<Name>());
        var broken = Phoenix2TitleList.BuildProgress(charts,
            new[] { Attempt(chart.Id, 820000, isBroken: true) }, new HashSet<Name>());

        Assert.True(passed.Single(p => p.Title.Name == "[PHOENIX] SINGLE BOSS BREAKER").IsComplete);
        Assert.False(broken.Single(p => p.Title.Name == "[PHOENIX] SINGLE BOSS BREAKER").IsComplete);
    }

    [Theory]
    [InlineData(28)]
    [InlineData(29)]
    public void TheLevellessPhoenixDoubleBossMatchesAnyLevelOf1948(int level)
    {
        // The [PHOENIX] double boss renders a "??" stepball — no parseable level.
        var chart = new ChartBuilder().WithSongName("1948").WithType(ChartType.Double)
            .WithLevel(level).Build();
        var charts = new Dictionary<Guid, Chart> { [chart.Id] = chart };

        var progress = Phoenix2TitleList.BuildProgress(charts,
            new[] { Attempt(chart.Id, 900000) }, new HashSet<Name>());

        Assert.True(progress.Single(p => p.Title.Name == "[PHOENIX] DOUBLE BOSS BREAKER").IsComplete);
    }

    [Fact]
    public void LadderExpertCountsItsTenMembers()
    {
        // Completing all ten [TWIST S] charts completes [TWIST S] EXPERT; nine does not.
        var songs = new[]
        {
            ("Scorpion King", 15), ("Street show down", 16), ("U Got Me Rocking", 17),
            ("Solitary 2", 18), ("U Got 2 Know", 19), ("Canon D", 20),
            ("Love Is A Danger Zone (Cranky Mix)", 21), ("DUEL", 21),
            ("Love is a Danger Zone pt.2", 22), ("Uranium", 22)
        };
        var charts = songs
            .Select(s => new ChartBuilder().WithSongName(s.Item1).WithType(ChartType.Single)
                .WithLevel(s.Item2).Build())
            .ToDictionary(c => c.Id);

        var allTen = Phoenix2TitleList.BuildProgress(charts,
            charts.Keys.Select(id => Attempt(id, 992000)).ToArray(), new HashSet<Name>());
        var nine = Phoenix2TitleList.BuildProgress(charts,
            charts.Keys.Take(9).Select(id => Attempt(id, 992000)).ToArray(), new HashSet<Name>());

        Assert.True(allTen.Single(p => p.Title.Name == "[TWIST S] EXPERT").IsComplete);
        var nineExpert = nine.Single(p => p.Title.Name == "[TWIST S] EXPERT");
        Assert.False(nineExpert.IsComplete);
        Assert.Equal(9, nineExpert.CompletionCount);
    }

    [Fact]
    public void SiteDetectedCompletionsCountTowardLadderExperts()
    {
        // No scores at all — ten site-detected [TWIST S] completions still complete EXPERT.
        var completed = Enumerable.Range(1, 10).Select(i => Name.From($"[TWIST S] LV.{i}")).ToHashSet();

        var progress = Phoenix2TitleList.BuildProgress(new Dictionary<Guid, Chart>(),
            Array.Empty<RecordedPhoenixScore>(), completed);

        Assert.True(progress.Single(p => p.Title.Name == "[TWIST S] EXPERT").IsComplete);
    }

    [Fact]
    public void SiteCompletionsApplyEvenWithoutAnyAttempts()
    {
        var progress = Phoenix2TitleList.BuildProgress(new Dictionary<Guid, Chart>(),
            Array.Empty<RecordedPhoenixScore>(), new HashSet<Name> { Name.From("BEGINNER") });

        Assert.True(progress.Single(p => p.Title.Name == "BEGINNER").IsComplete);
    }

    [Fact]
    public void SpecialistRequiresAllNinetySkillTitles()
    {
        var specialist = Phoenix2TitleList.GetTitleByName("SPECIALIST");
        Assert.Equal(90, specialist.CompletionRequired);
    }
}
