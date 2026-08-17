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
        // Unrounded. The singles pair prices one level up the base curve and lands on a fraction
        // — 784.16, where the pool used to report 784. The double sits on a whole number at this
        // level, which is why only one side of this moved.
        Assert.Equal(784.16, singlesLv1.CompletionCount, 2);
        Assert.Equal(377, doublesLv1.CompletionCount, 2);
        Assert.Equal(784.16 + 377, totalTier.CompletionCount, 2);
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

        Assert.Equal(19604, progress.Single(p => p.Title.Name == "[S] INTERMEDIATE LV.1").CompletionCount, 2);
    }

    [Fact]
    public void CoOpLadderProgressIsTheCoOpRatingAndNothingElseFeedsIt()
    {
        // An S RG duo (116.00) and an SSS+ UG trio (121.28) — the flat base pays a x3 the same
        // as a x2 — plus a broken co-op and a standard chart, neither of which is a co-op point.
        var duo = new ChartBuilder().WithType(ChartType.CoOp).WithLevel(2).Build();
        var trio = new ChartBuilder().WithType(ChartType.CoOp).WithLevel(3).Build();
        var walkoff = new ChartBuilder().WithType(ChartType.CoOp).WithLevel(2).Build();
        var single = new ChartBuilder().WithType(ChartType.Single).WithLevel(24).Build();
        var charts = new[] { duo, trio, walkoff, single }.ToDictionary(c => c.Id);

        var progress = Phoenix2TitleList.BuildProgress(charts, new[]
        {
            Attempt(duo.Id, 970000, plate: PhoenixPlate.RoughGame),
            Attempt(trio.Id, 995000, plate: PhoenixPlate.UltimateGame),
            Attempt(walkoff.Id, 990000, isBroken: true),
            Attempt(single.Id, 995000)
        }, new HashSet<Name>());

        var lv1 = progress.Single(p => p.Title.Name == "[CO-OP] LV.1");
        Assert.Equal(116.00 + 121.28, lv1.CompletionCount, 2);
        Assert.False(lv1.IsComplete);
        // And the co-ops stayed out of the pools.
        Assert.Equal(392.08, progress.Single(p => p.Title.Name == "[P.B] BRONZE").CompletionCount, 2);
    }

    [Fact]
    public void CoOpLadderSumsEveryChartWithNoTopFiftyCut()
    {
        // 87 charts at 121.60 apiece = 10,579.20: LV.10 (10,000) earned, ADVANCED (12,000) not — a
        // top-50 pool would stop at 6,080 and never reach LV.7. Every rung below LV.10 completes
        // with it, and each rung's floor is the rung beneath (LV.10 measures the climb from LV.9).
        var charts = Enumerable.Range(0, 87)
            .Select(_ => new ChartBuilder().WithType(ChartType.CoOp).WithLevel(2).Build())
            .ToDictionary(c => c.Id);

        var progress = Phoenix2TitleList.BuildProgress(charts,
            charts.Keys.Select(id => Attempt(id, 1000000, plate: PhoenixPlate.PerfectGame)).ToArray(),
            new HashSet<Name>());

        var lv10 = progress.Single(p => p.Title.Name == "[CO-OP] LV.10");
        Assert.Equal(87 * 121.60, lv10.CompletionCount, 2);
        Assert.True(lv10.IsComplete);
        Assert.Equal(9000, lv10.Title.CompletionFloor);
        Assert.False(progress.Single(p => p.Title.Name == "[CO-OP] ADVANCED").IsComplete);
        Assert.All(Enumerable.Range(1, 9),
            i => Assert.True(progress.Single(p => p.Title.Name == $"[CO-OP] LV.{i}").IsComplete));
    }

    [Fact]
    public void ASiteDetectedCoOpTitleStillCompletesWithoutTheScoresBehindIt()
    {
        // The site awards these itself; an account whose co-op plays never imported keeps what
        // it wears.
        var progress = Phoenix2TitleList.BuildProgress(new Dictionary<Guid, Chart>(),
            Array.Empty<RecordedPhoenixScore>(), new HashSet<Name> { Name.From("[CO-OP] LV.3") });

        Assert.True(progress.Single(p => p.Title.Name == "[CO-OP] LV.3").IsComplete);
        Assert.False(progress.Single(p => p.Title.Name == "[CO-OP] LV.4").IsComplete);
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
    [InlineData(29, true)]
    [InlineData(27, false)]
    [InlineData(24, false)]
    public void ThePhoenixDoubleBossIsThe1948D29Alone(int level, bool completes)
    {
        // The "??" stepball on the official page is how 1948 D29's level renders, not a wildcard:
        // the song's easier doubles charts must not hand out the title.
        var chart = new ChartBuilder().WithSongName("1948").WithType(ChartType.Double)
            .WithLevel(level).Build();
        var charts = new Dictionary<Guid, Chart> { [chart.Id] = chart };

        var progress = Phoenix2TitleList.BuildProgress(charts,
            new[] { Attempt(chart.Id, 900000) }, new HashSet<Name>());

        Assert.Equal(completes,
            progress.Single(p => p.Title.Name == "[PHOENIX] DOUBLE BOSS BREAKER").IsComplete);
    }

    [Fact]
    public void LadderExpertCountsItsTenMembers()
    {
        // Completing all ten [TWIST S] charts completes [TWIST S] EXPERT; nine does not. The song
        // names are the catalog's, not the official requirement page's abbreviations — a title
        // matches on the catalog spelling (TitleSongNameTests).
        var songs = new[]
        {
            ("Scorpion King", 15), ("Street show down", 16), ("U Got Me Rocking", 17),
            ("Solitary 2", 18), ("U Got 2 Know", 19), ("Canon D", 20),
            ("Love is a Danger Zone(Cranky Mix)", 21), ("DUEL", 21),
            ("Love is a Danger Zone pt. 2", 22), ("Uranium", 22)
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
