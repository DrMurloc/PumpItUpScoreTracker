using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using ScoreTracker.Catalog.Application;
using ScoreTracker.Catalog.Contracts;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.TestHelpers;
using Xunit;
using ChartSkillMetric = ScoreTracker.Catalog.Domain.ChartSkillMetric;
using ChartType = ScoreTracker.SharedKernel.Enums.ChartType;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class SearchChartsHandlerTests
{
    private readonly Mock<IChartRepository> _charts = new();
    private readonly Mock<ScoreTracker.Catalog.Domain.IChartSkillMetricRepository> _metrics = new();
    private readonly Mock<ITierListRepository> _tierLists = new();
    private readonly Mock<IChartScoringLevelRepository> _scoringLevels = new();
    private readonly Mock<IChartDifficultyRatingRepository> _ratings = new();
    private readonly Mock<IScoreReader> _scores = new();

    public SearchChartsHandlerTests()
    {
        _charts.Setup(c => c.GetCharts(It.IsAny<MixEnum>(), null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Chart>());
        _charts.Setup(c => c.GetChartMixLevels(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<(Guid, MixEnum, int)>());
        _metrics.Setup(m => m.GetMetricsByChart(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, IReadOnlyList<ChartSkillMetric>>());
        _tierLists.Setup(t => t.GetAllEntries(It.IsAny<MixEnum>(), It.IsAny<Name>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<SongTierListEntry>());
        _scoringLevels.Setup(s => s.GetScoringLevels(It.IsAny<MixEnum>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, double>());
        _ratings.Setup(r => r.GetAllChartRatedDifficulties(It.IsAny<MixEnum>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ChartDifficultyRatingRecord>());
        _scores.Setup(s => s.GetChartScoreAggregates(It.IsAny<MixEnum>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ChartScoreAggregate>());
        _scores.Setup(s => s.GetBestScores(It.IsAny<MixEnum>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<RecordedPhoenixScore>());
        _scores.Setup(s => s.GetBestXXAttempts(It.IsAny<MixEnum>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<BestXXChartAttempt>());
    }

    private SearchChartsHandler BuildHandler()
    {
        return new SearchChartsHandler(_charts.Object, _metrics.Object, _tierLists.Object, _scoringLevels.Object,
            _ratings.Object, _scores.Object, new MemoryCache(new MemoryCacheOptions()),
            FakeDateTime.At(2026, 7, 19, 9).Object);
    }

    private void SeedMix(MixEnum mix, params Chart[] charts)
    {
        _charts.Setup(c => c.GetCharts(mix, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(charts);
    }

    private static Chart MakeChart(Guid id, MixEnum mix, string song, int level,
        ChartType type = ChartType.Double, MixEnum? originalMix = null, LegacySlot? slot = null,
        string artist = "BanYa", SongType songType = SongType.Arcade, decimal bpm = 150,
        int? noteCount = null, int? seconds = null)
    {
        return new Chart(id, originalMix ?? mix,
            new Song(song, songType, new Uri("https://images.example/" + id + ".png"),
                TimeSpan.FromSeconds(seconds ?? 125), artist, Bpm.From(bpm, bpm)),
            type, level, mix, null, noteCount, new HashSet<Skill>(), slot);
    }

    [Fact]
    public async Task GroupsOneIdentityAcrossScopeMixesWithChronologicalAppearances()
    {
        var id = Guid.NewGuid();
        SeedMix(MixEnum.Phoenix, MakeChart(id, MixEnum.Phoenix, "Bee", 18));
        SeedMix(MixEnum.Phoenix2, MakeChart(id, MixEnum.Phoenix2, "Bee", 19));

        var result = await BuildHandler().Handle(new SearchChartsQuery
        {
            Mix = MixEnum.Phoenix2,
            Mixes = new[] { MixEnum.Phoenix2, MixEnum.Phoenix }
        }, CancellationToken.None);

        var row = Assert.Single(result.Results);
        Assert.Equal(MixEnum.Phoenix2, row.Chart.Mix);
        Assert.Equal(new[] { MixEnum.Phoenix, MixEnum.Phoenix2 }, row.Appearances.Select(a => a.Mix));
        Assert.Equal(MixEnum.Phoenix2, row.LatestMix);
        Assert.Equal(1, row.LevelChange);
    }

    [Fact]
    public async Task LinksToTheLatestAppearanceWhenAbsentFromTheVisitorsMix()
    {
        var id = Guid.NewGuid();
        SeedMix(MixEnum.XX, MakeChart(id, MixEnum.XX, "Canon-D", 19));
        SeedMix(MixEnum.Phoenix, MakeChart(id, MixEnum.Phoenix, "Canon-D", 19));

        var result = await BuildHandler().Handle(new SearchChartsQuery
        {
            Mix = MixEnum.Phoenix2,
            Mixes = new[] { MixEnum.XX, MixEnum.Phoenix }
        }, CancellationToken.None);

        Assert.Equal(MixEnum.Phoenix, Assert.Single(result.Results).Chart.Mix);
    }

    [Fact]
    public async Task DefaultSortIsLevelDescendingWithScoringLevelTiebreak()
    {
        var easy21 = Guid.NewGuid();
        var hard21 = Guid.NewGuid();
        var lone20 = Guid.NewGuid();
        SeedMix(MixEnum.Phoenix,
            MakeChart(easy21, MixEnum.Phoenix, "Easy One", 21),
            MakeChart(hard21, MixEnum.Phoenix, "Hard One", 21),
            MakeChart(lone20, MixEnum.Phoenix, "Lower", 20));
        _scoringLevels.Setup(s => s.GetScoringLevels(MixEnum.Phoenix, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, double> { [easy21] = 20.7, [hard21] = 21.9 });

        var result = await BuildHandler().Handle(new SearchChartsQuery { Mix = MixEnum.Phoenix },
            CancellationToken.None);

        Assert.Equal(new[] { hard21, easy21, lone20 }, result.Results.Select(r => r.Chart.Id));
    }

    [Fact]
    public async Task ScoringLevelSortUsesCommunityVoteForLegacyResults()
    {
        var mild = Guid.NewGuid();
        var brutal = Guid.NewGuid();
        SeedMix(MixEnum.XX,
            MakeChart(brutal, MixEnum.XX, "Brutal", 16),
            MakeChart(mild, MixEnum.XX, "Mild", 16));
        _ratings.Setup(r => r.GetAllChartRatedDifficulties(MixEnum.XX, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new ChartDifficultyRatingRecord(mild, 15.4, 12, 0),
                new ChartDifficultyRatingRecord(brutal, 16.9, 12, 0)
            });

        var result = await BuildHandler().Handle(new SearchChartsQuery
        {
            Mix = MixEnum.XX,
            Sort = ChartSearchSort.ScoringLevel,
            SortDescending = false
        }, CancellationToken.None);

        Assert.Equal(new[] { mild, brutal }, result.Results.Select(r => r.Chart.Id));
        Assert.Equal(15.4, result.Results[0].CommunityVoteRating);
        // The scoring-level projection stays empty for legacy results — votes fill their own field.
        Assert.Null(result.Results[0].ScoringLevel);
    }

    [Fact]
    public async Task TierSourceSplitsByScoringFamily()
    {
        var phoenixChart = Guid.NewGuid();
        var legacyChart = Guid.NewGuid();
        SeedMix(MixEnum.Phoenix, MakeChart(phoenixChart, MixEnum.Phoenix, "Modern", 20));
        SeedMix(MixEnum.XX, MakeChart(legacyChart, MixEnum.XX, "Old", 20));
        _tierLists.Setup(t => t.GetAllEntries(MixEnum.Phoenix, It.Is<Name>(n => n == "Pass Count"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new SongTierListEntry("Pass Count", phoenixChart, TierListCategory.Hard, 1) });
        _tierLists.Setup(t => t.GetAllEntries(MixEnum.Phoenix, It.Is<Name>(n => n == "Scores"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new SongTierListEntry("Scores", phoenixChart, TierListCategory.Easy, 1) });
        _tierLists.Setup(t => t.GetAllEntries(MixEnum.XX, It.Is<Name>(n => n == "Difficulty"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new SongTierListEntry("Difficulty", legacyChart, TierListCategory.VeryHard, 1) });

        var result = await BuildHandler().Handle(new SearchChartsQuery
        {
            Mix = MixEnum.Phoenix,
            Mixes = new[] { MixEnum.Phoenix, MixEnum.XX }
        }, CancellationToken.None);

        var modern = result.Results.Single(r => r.Chart.Id == phoenixChart);
        var legacy = result.Results.Single(r => r.Chart.Id == legacyChart);
        Assert.Equal(TierListCategory.Hard, modern.PassDifficulty);
        Assert.Equal(TierListCategory.Easy, modern.ScoreDifficulty);
        Assert.Null(modern.CommunityVote);
        Assert.Equal(TierListCategory.VeryHard, legacy.CommunityVote);
        Assert.Null(legacy.PassDifficulty);
        _tierLists.Verify(t => t.GetAllEntries(MixEnum.Phoenix, It.Is<Name>(n => n == "Difficulty"),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LevelRangeMatchesAnyInScopeAppearance()
    {
        var rerated = Guid.NewGuid();
        var tooHigh = Guid.NewGuid();
        SeedMix(MixEnum.Phoenix,
            MakeChart(rerated, MixEnum.Phoenix, "Moved", 18),
            MakeChart(tooHigh, MixEnum.Phoenix, "High", 20));
        SeedMix(MixEnum.Phoenix2,
            MakeChart(rerated, MixEnum.Phoenix2, "Moved", 19),
            MakeChart(tooHigh, MixEnum.Phoenix2, "High", 20));

        var result = await BuildHandler().Handle(new SearchChartsQuery
        {
            Mix = MixEnum.Phoenix2,
            Mixes = new[] { MixEnum.Phoenix, MixEnum.Phoenix2 },
            LevelMin = 18,
            LevelMax = 18
        }, CancellationToken.None);

        Assert.Equal(rerated, Assert.Single(result.Results).Chart.Id);
    }

    [Fact]
    public async Task BadgeFilterMatchesHighlightedTopThreeOnly()
    {
        var dominant = Guid.NewGuid();
        var containsOnly = Guid.NewGuid();
        SeedMix(MixEnum.Phoenix,
            MakeChart(dominant, MixEnum.Phoenix, "Dominant", 20),
            MakeChart(containsOnly, MixEnum.Phoenix, "Contains", 20));
        _metrics.Setup(m => m.GetMetricsByChart("PiuCenter", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, IReadOnlyList<ChartSkillMetric>>
            {
                [dominant] = new[] { new ChartSkillMetric(dominant, "top3:drill", 1m, null) },
                [containsOnly] = new[] { new ChartSkillMetric(containsOnly, "badge_fraction:drill", 0.9m, null) }
            });

        var result = await BuildHandler().Handle(new SearchChartsQuery
        {
            Mix = MixEnum.Phoenix,
            Badges = new[] { "drill" }
        }, CancellationToken.None);

        var row = Assert.Single(result.Results);
        Assert.Equal(dominant, row.Chart.Id);
        Assert.Equal("Drills", Assert.Single(row.Badges).DisplayName);
    }

    [Fact]
    public async Task NpsFilterSilentlyExcludesUnmatchedCharts()
    {
        var matched = Guid.NewGuid();
        var unmatched = Guid.NewGuid();
        SeedMix(MixEnum.Phoenix,
            MakeChart(matched, MixEnum.Phoenix, "Matched", 20),
            MakeChart(unmatched, MixEnum.Phoenix, "Legacy Import", 20));
        _metrics.Setup(m => m.GetMetricsByChart("PiuCenter", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, IReadOnlyList<ChartSkillMetric>>
            {
                [matched] = new[] { new ChartSkillMetric(matched, "nps", 12.5m, null) }
            });

        var result = await BuildHandler().Handle(new SearchChartsQuery
        {
            Mix = MixEnum.Phoenix,
            NpsMin = 10
        }, CancellationToken.None);

        Assert.Equal(matched, Assert.Single(result.Results).Chart.Id);
    }

    [Fact]
    public async Task ReratedFiltersReadTheSignedLevelChange()
    {
        var up = Guid.NewGuid();
        var down = Guid.NewGuid();
        var flat = Guid.NewGuid();
        SeedMix(MixEnum.Phoenix,
            MakeChart(up, MixEnum.Phoenix, "Buffed", 18),
            MakeChart(down, MixEnum.Phoenix, "Nerfed", 20),
            MakeChart(flat, MixEnum.Phoenix, "Same", 19));
        SeedMix(MixEnum.Phoenix2,
            MakeChart(up, MixEnum.Phoenix2, "Buffed", 19),
            MakeChart(down, MixEnum.Phoenix2, "Nerfed", 19),
            MakeChart(flat, MixEnum.Phoenix2, "Same", 19));

        var query = new SearchChartsQuery
        {
            Mix = MixEnum.Phoenix2,
            Mixes = new[] { MixEnum.Phoenix, MixEnum.Phoenix2 }
        };
        var handler = BuildHandler();

        var upOnly = await handler.Handle(query with { ReratedUp = true }, CancellationToken.None);
        var either = await handler.Handle(query with { ReratedUp = true, ReratedDown = true },
            CancellationToken.None);

        Assert.Equal(up, Assert.Single(upOnly.Results).Chart.Id);
        Assert.Equal(2, either.Results.Count);
        Assert.DoesNotContain(either.Results, r => r.Chart.Id == flat);
    }

    [Fact]
    public async Task AvailabilityFiltersFindTheCutContent()
    {
        var cut = Guid.NewGuid();
        var kept = Guid.NewGuid();
        SeedMix(MixEnum.Phoenix,
            MakeChart(cut, MixEnum.Phoenix, "Cut", 19),
            MakeChart(kept, MixEnum.Phoenix, "Kept", 19));
        SeedMix(MixEnum.Phoenix2, MakeChart(kept, MixEnum.Phoenix2, "Kept", 19));

        var result = await BuildHandler().Handle(new SearchChartsQuery
        {
            Mix = MixEnum.Phoenix,
            Mixes = new[] { MixEnum.Phoenix, MixEnum.Phoenix2 },
            AvailableIn = MixEnum.Phoenix,
            NotAvailableIn = MixEnum.Phoenix2
        }, CancellationToken.None);

        Assert.Equal(cut, Assert.Single(result.Results).Chart.Id);
    }

    [Fact]
    public async Task PagingSlicesAfterTheTotalIsCounted()
    {
        SeedMix(MixEnum.Phoenix,
            MakeChart(Guid.NewGuid(), MixEnum.Phoenix, "Alpha", 20),
            MakeChart(Guid.NewGuid(), MixEnum.Phoenix, "Beta", 20),
            MakeChart(Guid.NewGuid(), MixEnum.Phoenix, "Gamma", 20));
        var handler = BuildHandler();

        var firstPage = await handler.Handle(new SearchChartsQuery { Mix = MixEnum.Phoenix, PageSize = 2 },
            CancellationToken.None);
        var unpaged = await handler.Handle(new SearchChartsQuery { Mix = MixEnum.Phoenix, Page = null },
            CancellationToken.None);

        Assert.Equal(3, firstPage.TotalCount);
        Assert.Equal(2, firstPage.Results.Count);
        Assert.Equal(3, unpaged.Results.Count);
    }

    [Fact]
    public async Task PassRateNeedsTheMinimumSample()
    {
        var attested = Guid.NewGuid();
        var thin = Guid.NewGuid();
        SeedMix(MixEnum.Phoenix,
            MakeChart(attested, MixEnum.Phoenix, "Attested", 20),
            MakeChart(thin, MixEnum.Phoenix, "Thin", 20));
        _scores.Setup(s => s.GetChartScoreAggregates(MixEnum.Phoenix, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new ChartScoreAggregate(attested, 20, 15),
                new ChartScoreAggregate(thin, 5, 5)
            });

        var result = await BuildHandler().Handle(new SearchChartsQuery
        {
            Mix = MixEnum.Phoenix,
            PassRateMin = 0.5
        }, CancellationToken.None);

        Assert.Equal(attested, Assert.Single(result.Results).Chart.Id);
    }

    [Fact]
    public async Task SongNameContainsIsACaseInsensitiveSubstringMatch()
    {
        SeedMix(MixEnum.Phoenix,
            MakeChart(Guid.NewGuid(), MixEnum.Phoenix, "District 1", 20),
            MakeChart(Guid.NewGuid(), MixEnum.Phoenix, "Bee", 20));

        var result = await BuildHandler().Handle(new SearchChartsQuery
        {
            Mix = MixEnum.Phoenix,
            SongNameContains = "istri"
        }, CancellationToken.None);

        Assert.Equal("District 1", Assert.Single(result.Results).Chart.Song.Name.ToString());
    }

    private static readonly Guid User = Guid.NewGuid();

    private void SeedPhoenixRecords(MixEnum mix, params RecordedPhoenixScore[] records)
    {
        _scores.Setup(s => s.GetBestScores(mix, User, It.IsAny<CancellationToken>()))
            .ReturnsAsync(records);
    }

    private void SeedLegacyRecords(MixEnum mix, params BestXXChartAttempt[] attempts)
    {
        _scores.Setup(s => s.GetBestXXAttempts(mix, User, It.IsAny<CancellationToken>()))
            .ReturnsAsync(attempts);
    }

    [Fact]
    public async Task MyStateShowsTheLinkedBestAndTheCrossMixPassMarker()
    {
        var id = Guid.NewGuid();
        SeedMix(MixEnum.Phoenix, MakeChart(id, MixEnum.Phoenix, "Bee", 19));
        SeedMix(MixEnum.Phoenix2, MakeChart(id, MixEnum.Phoenix2, "Bee", 19));
        SeedPhoenixRecords(MixEnum.Phoenix,
            new RecordedPhoenixScore(id, PhoenixScore.From(977210), PhoenixPlate.SuperbGame, false,
                new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero)));
        SeedPhoenixRecords(MixEnum.Phoenix2);

        var result = await BuildHandler().Handle(new SearchChartsQuery
        {
            Mix = MixEnum.Phoenix2,
            Mixes = new[] { MixEnum.Phoenix, MixEnum.Phoenix2 },
            UserId = User
        }, CancellationToken.None);

        var my = Assert.Single(result.Results).My;
        Assert.NotNull(my);
        Assert.Null(my!.PhoenixScore);
        Assert.False(my.PassedInLinkedMix);
        Assert.True(my.PassedInAnotherScopeMix);
    }

    [Fact]
    public async Task ScoreStateFiltersJudgeTheLinkedAppearance()
    {
        var played = Guid.NewGuid();
        var untouched = Guid.NewGuid();
        SeedMix(MixEnum.Phoenix,
            MakeChart(played, MixEnum.Phoenix, "Played", 19),
            MakeChart(untouched, MixEnum.Phoenix, "Untouched", 19));
        SeedPhoenixRecords(MixEnum.Phoenix,
            new RecordedPhoenixScore(played, PhoenixScore.From(912000), null, true,
                new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero)));
        var handler = BuildHandler();
        var query = new SearchChartsQuery { Mix = MixEnum.Phoenix, UserId = User };

        var unplayed = await handler.Handle(query with { ScoreState = ChartScoreStateFilter.Unplayed },
            CancellationToken.None);
        var failed = await handler.Handle(query with { ScoreState = ChartScoreStateFilter.Failed },
            CancellationToken.None);
        var passed = await handler.Handle(query with { ScoreState = ChartScoreStateFilter.Passed },
            CancellationToken.None);

        Assert.Equal(untouched, Assert.Single(unplayed.Results).Chart.Id);
        Assert.Equal(played, Assert.Single(failed.Results).Chart.Id);
        Assert.Empty(passed.Results);
    }

    [Fact]
    public async Task ReclearGapFindsPassesThatHaveNotLandedInTheTargetMix()
    {
        var gap = Guid.NewGuid();
        var recleared = Guid.NewGuid();
        var neverPassed = Guid.NewGuid();
        SeedMix(MixEnum.Phoenix,
            MakeChart(gap, MixEnum.Phoenix, "Gap", 19),
            MakeChart(recleared, MixEnum.Phoenix, "Recleared", 19),
            MakeChart(neverPassed, MixEnum.Phoenix, "Never", 19));
        SeedMix(MixEnum.Phoenix2,
            MakeChart(gap, MixEnum.Phoenix2, "Gap", 19),
            MakeChart(recleared, MixEnum.Phoenix2, "Recleared", 19),
            MakeChart(neverPassed, MixEnum.Phoenix2, "Never", 19));
        var may = new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero);
        SeedPhoenixRecords(MixEnum.Phoenix,
            new RecordedPhoenixScore(gap, PhoenixScore.From(960000), null, false, may),
            new RecordedPhoenixScore(recleared, PhoenixScore.From(950000), null, false, may));
        SeedPhoenixRecords(MixEnum.Phoenix2,
            new RecordedPhoenixScore(recleared, PhoenixScore.From(940000), null, false, may));

        var result = await BuildHandler().Handle(new SearchChartsQuery
        {
            Mix = MixEnum.Phoenix2,
            Mixes = new[] { MixEnum.Phoenix, MixEnum.Phoenix2 },
            UserId = User,
            NotReclearedIn = MixEnum.Phoenix2
        }, CancellationToken.None);

        Assert.Equal(gap, Assert.Single(result.Results).Chart.Id);
    }

    [Fact]
    public async Task FamilyScoreFiltersJudgeEachRowByItsOwnFamilyOnly()
    {
        var modern = Guid.NewGuid();
        var legacy = Guid.NewGuid();
        SeedMix(MixEnum.Phoenix, MakeChart(modern, MixEnum.Phoenix, "Modern", 19));
        SeedMix(MixEnum.XX, MakeChart(legacy, MixEnum.XX, "Old", 19));
        var may = new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero);
        SeedPhoenixRecords(MixEnum.Phoenix,
            new RecordedPhoenixScore(modern, PhoenixScore.From(996000), null, false, may));
        SeedLegacyRecords(MixEnum.XX,
            new BestXXChartAttempt(MakeChart(legacy, MixEnum.XX, "Old", 19),
                new XXChartAttempt(XXLetterGrade.S, false, 92410, may)));
        var handler = BuildHandler();
        var query = new SearchChartsQuery
        {
            Mix = MixEnum.Phoenix,
            Mixes = new[] { MixEnum.XX, MixEnum.Phoenix },
            UserId = User
        };

        var phoenixOnly = await handler.Handle(query with { PhoenixGradeMin = PhoenixLetterGrade.SSS },
            CancellationToken.None);
        var bothFamilies = await handler.Handle(query with
        {
            PhoenixGradeMin = PhoenixLetterGrade.SSS,
            LegacyGradeMin = XXLetterGrade.S
        }, CancellationToken.None);
        var legacyTooHigh = await handler.Handle(query with { LegacyGradeMin = XXLetterGrade.SSS },
            CancellationToken.None);

        Assert.Equal(modern, Assert.Single(phoenixOnly.Results).Chart.Id);
        Assert.Equal(2, bothFamilies.Results.Count);
        Assert.Empty(legacyTooHigh.Results);
    }

    [Fact]
    public async Task RecordedDateRangeReadsTheLinkedRecord()
    {
        var recent = Guid.NewGuid();
        var stale = Guid.NewGuid();
        SeedMix(MixEnum.Phoenix,
            MakeChart(recent, MixEnum.Phoenix, "Recent", 19),
            MakeChart(stale, MixEnum.Phoenix, "Stale", 19));
        SeedPhoenixRecords(MixEnum.Phoenix,
            new RecordedPhoenixScore(recent, PhoenixScore.From(950000), null, false,
                new DateTimeOffset(2026, 7, 10, 0, 0, 0, TimeSpan.Zero)),
            new RecordedPhoenixScore(stale, PhoenixScore.From(950000), null, false,
                new DateTimeOffset(2026, 1, 10, 0, 0, 0, TimeSpan.Zero)));

        var result = await BuildHandler().Handle(new SearchChartsQuery
        {
            Mix = MixEnum.Phoenix,
            UserId = User,
            RecordedFrom = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero)
        }, CancellationToken.None);

        Assert.Equal(recent, Assert.Single(result.Results).Chart.Id);
    }

    [Fact]
    public async Task CommunityBundlesAreCachedBetweenSearches()
    {
        SeedMix(MixEnum.Phoenix, MakeChart(Guid.NewGuid(), MixEnum.Phoenix, "Cached", 20));
        var handler = BuildHandler();

        await handler.Handle(new SearchChartsQuery { Mix = MixEnum.Phoenix }, CancellationToken.None);
        await handler.Handle(new SearchChartsQuery { Mix = MixEnum.Phoenix }, CancellationToken.None);

        _tierLists.Verify(t => t.GetAllEntries(MixEnum.Phoenix, It.Is<Name>(n => n == "Pass Count"),
            It.IsAny<CancellationToken>()), Times.Once);
        _scores.Verify(s => s.GetChartScoreAggregates(MixEnum.Phoenix, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
