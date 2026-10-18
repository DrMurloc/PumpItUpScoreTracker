using ScoreTracker.Domain.Models.Titles;
using ScoreTracker.Domain.Models.Titles.Phoenix;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Services;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     Which chart a chart title's drawer draws. Matched the way the title is earned, so these pin
///     that the card can never point at a near miss — the same song at another level, or the other
///     half of a boss pair.
/// </summary>
public sealed class TitleChartsTests
{
    private static readonly PhoenixBossBreakerTitle XxSingle = new("XX", "ERRORCODE: 0", ChartType.Single, 25);

    private static Chart Chart(string song, ChartType type, int level) =>
        new(Guid.NewGuid(), MixEnum.Phoenix,
            new Song(song, SongType.Arcade, new Uri("https://piu.test/art.png"),
                TimeSpan.FromMinutes(2), "Artist", Bpm.From(200, 200)),
            type, level, MixEnum.Phoenix, null, 900);

    [Fact]
    public void AChartTitleResolvesToExactlyTheChartItIsEarnedOn()
    {
        var single = Chart("ERRORCODE: 0", ChartType.Single, 25);
        var charts = new[]
        {
            Chart("ERRORCODE: 0", ChartType.Double, 25), single, Chart("ERRORCODE: 0", ChartType.Single, 24),
            Chart("1949", ChartType.Double, 28)
        };

        var resolved = TitleCharts.Resolve(new Title[] { XxSingle }, charts);

        Assert.Same(single, resolved[XxSingle.Name]);
    }

    [Fact]
    public void TheSameSongAtAnotherLevelIsNotTheChart()
    {
        var resolved = TitleCharts.Resolve(new Title[] { XxSingle },
            new[] { Chart("ERRORCODE: 0", ChartType.Single, 24) });

        Assert.Empty(resolved);
    }

    [Fact]
    public void ATitleThatNamesNoChartResolvesToNothing()
    {
        var basic = new PhoenixBasicTitle("[DRILL] EXPERT", "Earn Drill Titles 1-10", "Skill", "DRILL", 11);

        var resolved = TitleCharts.Resolve(new Title[] { basic }, new[] { Chart("ERRORCODE: 0", ChartType.Single, 25) });

        Assert.Empty(resolved);
    }

    [Fact]
    public void ASongNameMatchesWhateverItsCase()
    {
        var chart = Chart("errorcode: 0", ChartType.Single, 25);

        var resolved = TitleCharts.Resolve(new Title[] { XxSingle }, new[] { chart });

        Assert.Same(chart, resolved[XxSingle.Name]);
    }
}
