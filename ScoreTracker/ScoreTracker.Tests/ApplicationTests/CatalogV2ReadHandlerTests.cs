using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ScoreTracker.Catalog.Application;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.Catalog.Domain;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Domain.SecondaryPorts;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

/// <summary>
///     The three Catalog reads api/v2 needs that nothing else exposed.
/// </summary>
public sealed class CatalogV2ReadHandlerTests
{
    private static Chart ChartFor(Song song, ChartType type = ChartType.Single, int level = 17)
    {
        return new Chart(Guid.NewGuid(), MixEnum.Phoenix, song, type, DifficultyLevel.From(level),
            MixEnum.Phoenix, null, 500, new HashSet<Skill>());
    }

    private static Song SongFor(string name, decimal? bpm = null)
    {
        return new Song(Name.From(name), SongType.Arcade, new Uri("https://example.com/a.png"),
            TimeSpan.FromSeconds(100), Name.From("BanYa"), Bpm.From(bpm, bpm));
    }

    [Fact]
    public async Task EveryMixIsReturnedOldestFirst()
    {
        var mixes = await new GetMixesHandler().Handle(new GetMixesQuery(), CancellationToken.None);

        Assert.Equal(Enum.GetValues<MixEnum>().Length, mixes.Count);
        Assert.Equal(mixes.OrderBy(m => m.SortOrder).Select(m => m.Mix), mixes.Select(m => m.Mix));
    }

    // The field a consumer must branch on before reading any score.
    [Fact]
    public async Task OnlyPhoenixMixesReportModernScoring()
    {
        var mixes = await new GetMixesHandler().Handle(new GetMixesQuery(), CancellationToken.None);

        Assert.All(mixes.Where(m => m.Mix is MixEnum.Phoenix or MixEnum.Phoenix2),
            m => Assert.False(m.UsesLegacyScoring));
        Assert.All(mixes.Where(m => m.Mix is not (MixEnum.Phoenix or MixEnum.Phoenix2)),
            m => Assert.True(m.UsesLegacyScoring));
    }

    [Fact]
    public async Task MixNameIsTheEnumNameAndDisplayNameIsTheHumanOne()
    {
        var mixes = await new GetMixesHandler().Handle(new GetMixesQuery(), CancellationToken.None);
        var phoenix2 = mixes.Single(m => m.Mix == MixEnum.Phoenix2);

        Assert.Equal("Phoenix2", phoenix2.Name);
        Assert.Equal("Phoenix 2", phoenix2.DisplayName);
    }

    [Fact]
    public async Task SongsAreDedupedAcrossTheirChartsAndCarryFullMetadata()
    {
        var bee = SongFor("Bee", 140m);
        var charts = new Mock<IChartRepository>();
        charts.Setup(c => c.GetCharts(MixEnum.Phoenix, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                ChartFor(bee), ChartFor(bee, ChartType.Double, 21), ChartFor(SongFor("Alfa"))
            });

        var songs = await new GetSongsHandler(charts.Object)
            .Handle(new GetSongsQuery(MixEnum.Phoenix), CancellationToken.None);

        Assert.Equal(new[] { "Alfa", "Bee" }, songs.Select(s => s.Name.ToString()));
        var song = songs.Single(s => s.Name == Name.From("Bee"));
        Assert.Equal("BanYa", song.Artist.ToString());
        Assert.Equal(TimeSpan.FromSeconds(100), song.Duration);
        Assert.Equal(140m, song.MinBpm);
    }

    [Fact]
    public async Task SongWithoutBpmReportsNullRatherThanZero()
    {
        var charts = new Mock<IChartRepository>();
        charts.Setup(c => c.GetCharts(MixEnum.Phoenix, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { ChartFor(SongFor("Alfa")) });

        var songs = await new GetSongsHandler(charts.Object)
            .Handle(new GetSongsQuery(MixEnum.Phoenix), CancellationToken.None);

        Assert.Null(songs.Single().MinBpm);
        Assert.Null(songs.Single().MaxBpm);
    }

    private static Mock<IChartSkillMetricRepository> MetricsFor(Guid chartId,
        params (string Name, decimal Value)[] metrics)
    {
        var repo = new Mock<IChartSkillMetricRepository>();
        repo.Setup(r => r.GetMetricsByChart(PiuCenterMetrics.Source, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, IReadOnlyList<ChartSkillMetric>>
            {
                [chartId] = metrics.Select(m => new ChartSkillMetric(chartId, m.Name, m.Value, null)).ToArray()
            });
        return repo;
    }

    [Fact]
    public async Task ScalarMetricsAreLiftedOutOfTheBag()
    {
        var chartId = Guid.NewGuid();
        var repo = MetricsFor(chartId,
            (PiuCenterMetrics.Nps, 8.4m),
            (PiuCenterMetrics.DifficultyPrediction, 18.4m),
            (PiuCenterMetrics.SustainTime, 122m),
            (PiuCenterMetrics.TimeUnderTension, 140m),
            (PiuCenterMetrics.DataVersion, 50726m),
            (PiuCenterMetrics.LastSegmentIsPeak, 1m));

        var profile = (await new GetChartSkillProfilesHandler(repo.Object)
            .Handle(new GetChartSkillProfilesQuery(), CancellationToken.None)).Single();

        Assert.Equal(8.4, profile.Nps);
        Assert.Equal(18.4, profile.DifficultyPrediction);
        Assert.Equal(122, profile.SustainTimeSeconds);
        Assert.Equal(140, profile.TimeUnderTensionSeconds);
        Assert.Equal(50726, profile.DataVersion);
        Assert.True(profile.LastSegmentIsPeak);
    }

    [Fact]
    public async Task PerSkillFamiliesJoinOnTheSkillName()
    {
        var chartId = Guid.NewGuid();
        var repo = MetricsFor(chartId,
            (PiuCenterMetrics.BadgeFractionPrefix + "twist_over90", 0.625m),
            (PiuCenterMetrics.Top3Prefix + "twist_over90", 1m),
            (PiuCenterMetrics.PracticeRankPrefix + "twist_over90", 4m),
            (PiuCenterMetrics.LastSegmentPrefix + "twist_over90", 1m));

        var profile = (await new GetChartSkillProfilesHandler(repo.Object)
            .Handle(new GetChartSkillProfilesQuery(), CancellationToken.None)).Single();

        var skill = Assert.Single(profile.Skills);
        Assert.Equal("twist_over90", skill.Name);
        Assert.Equal(0.625, skill.Fraction);
        Assert.Equal(1, skill.Top3Rank);
        Assert.Equal(4, skill.PracticeRank);
        Assert.True(skill.InLastSegment);
    }

    // A top-3 pick can name a skill the chart has no coverage row for — Dream To Nightmare's
    // picks are anchor_run/bursty/split despite heavy bracket coverage. Dropping the skill
    // because one family is silent would hide the pick.
    [Fact]
    public async Task SkillAppearsWhenOnlyOneFamilyMentionsIt()
    {
        var chartId = Guid.NewGuid();
        var repo = MetricsFor(chartId, (PiuCenterMetrics.Top3Prefix + "split", 3m));

        var profile = (await new GetChartSkillProfilesHandler(repo.Object)
            .Handle(new GetChartSkillProfilesQuery(), CancellationToken.None)).Single();

        var skill = Assert.Single(profile.Skills);
        Assert.Equal("split", skill.Name);
        Assert.Null(skill.Fraction);
        Assert.Equal(3, skill.Top3Rank);
        Assert.False(skill.InLastSegment);
    }

    // "rare:bracket-5" is a pattern with an occurrence count, not a skill.
    [Fact]
    public async Task RarePatternsAreNotTreatedAsSkills()
    {
        var chartId = Guid.NewGuid();
        var repo = MetricsFor(chartId, (PiuCenterMetrics.RarePrefix + "bracket-5", 3m));

        var profile = (await new GetChartSkillProfilesHandler(repo.Object)
            .Handle(new GetChartSkillProfilesQuery(), CancellationToken.None)).Single();

        Assert.Empty(profile.Skills);
        var rare = Assert.Single(profile.RarePatterns);
        Assert.Equal("bracket-5", rare.Name);
        Assert.Equal(3, rare.Count);
    }

    [Fact]
    public async Task ChartWithoutAnalysisReportsNullsRatherThanZeros()
    {
        var chartId = Guid.NewGuid();
        var repo = MetricsFor(chartId, (PiuCenterMetrics.BadgeFractionPrefix + "jump", 0.5m));

        var profile = (await new GetChartSkillProfilesHandler(repo.Object)
            .Handle(new GetChartSkillProfilesQuery(), CancellationToken.None)).Single();

        Assert.Null(profile.Nps);
        Assert.Null(profile.DifficultyPrediction);
        Assert.Null(profile.LastSegmentIsPeak);
    }
}
