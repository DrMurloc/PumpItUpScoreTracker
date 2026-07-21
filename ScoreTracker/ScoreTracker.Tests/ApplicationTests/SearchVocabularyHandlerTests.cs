using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ScoreTracker.Catalog.Application;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.Catalog.Domain;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using Xunit;
using ChartType = ScoreTracker.SharedKernel.Enums.ChartType;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class SearchVocabularyHandlerTests
{
    private readonly Mock<IChartRepository> _charts = new();
    private readonly Mock<IChartSkillMetricRepository> _metrics = new();
    private readonly Mock<IChartScoringLevelRepository> _scoringLevels = new();

    public SearchVocabularyHandlerTests()
    {
        _charts.Setup(c => c.GetChartMixLevels(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<(Guid, MixEnum, int)>());
        _metrics.Setup(m => m.GetMetricsByChart(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, IReadOnlyList<ChartSkillMetric>>());
        _scoringLevels.Setup(s => s.GetScoringLevels(It.IsAny<MixEnum>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, double>());
        _charts.Setup(c => c.GetCharts(It.IsAny<MixEnum>(), null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Chart>());
    }

    private SearchVocabularyHandler BuildHandler()
    {
        return new SearchVocabularyHandler(_charts.Object, _metrics.Object, _scoringLevels.Object);
    }

    private static Chart MakeChart(string song, string artist, string? stepArtist, MixEnum mix)
    {
        return new Chart(Guid.NewGuid(), mix,
            new Song(song, SongType.Arcade, new Uri("https://piu.test/a.png"), TimeSpan.FromMinutes(2),
                artist, Bpm.From(150, 150)),
            ChartType.Double, 19, mix, stepArtist == null ? (Name?)null : Name.From(stepArtist), null,
            new HashSet<Skill>());
    }

    [Fact]
    public async Task TheBadgeCloudIsTheDistinctTopThreeVocabularyDisplayNamed()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        _metrics.Setup(m => m.GetMetricsByChart("PiuCenter", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, IReadOnlyList<ChartSkillMetric>>
            {
                [a] = new[]
                {
                    new ChartSkillMetric(a, "top3:drill", 1m, null),
                    new ChartSkillMetric(a, "badge_fraction:jump", 0.9m, null),
                    new ChartSkillMetric(a, "nps", 11m, null)
                },
                [b] = new[]
                {
                    new ChartSkillMetric(b, "top3:drill", 1m, null),
                    new ChartSkillMetric(b, "top3:twist_over90", 1m, null)
                }
            });

        var badges = await BuildHandler().Handle(new GetSearchBadgesQuery(), CancellationToken.None);

        Assert.Equal(2, badges.Count);
        Assert.Contains(badges, x => x is { Key: "drill", DisplayName: "Drills" });
        Assert.Contains(badges, x => x is { Key: "twist_over90", DisplayName: "Over-90 Twists" });
        // Contains-level and non-badge metrics never reach the facet.
        Assert.DoesNotContain(badges, x => x.Key == "jump" || x.Key == "nps");
    }

    [Fact]
    public async Task ArtistDictionariesAreDistinctAcrossTheScope()
    {
        _charts.Setup(c => c.GetCharts(MixEnum.Phoenix, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                MakeChart("A", "BanYa", "EXC", MixEnum.Phoenix),
                MakeChart("B", "banya", null, MixEnum.Phoenix),
                MakeChart("C", "Doin", "SPHAM", MixEnum.Phoenix)
            });

        var artists = await BuildHandler().Handle(new GetSearchArtistsQuery(MixEnum.Phoenix),
            CancellationToken.None);
        var stepArtists = await BuildHandler().Handle(new GetSearchStepArtistsQuery(MixEnum.Phoenix),
            CancellationToken.None);

        Assert.Equal(2, artists.Count);
        Assert.Equal(new[] { "EXC", "SPHAM" }, stepArtists.OrderBy(s => s));
    }
}
