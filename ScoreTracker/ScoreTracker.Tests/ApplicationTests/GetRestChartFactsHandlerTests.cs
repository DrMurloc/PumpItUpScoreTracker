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

namespace ScoreTracker.Tests.ApplicationTests;

/// <summary>
///     Rest charts are rest relative to their own folder — mix, chart type and level (D29) — so the
///     handler's job is building the right distribution for each chart asked about and answering
///     from it. The rule itself is <see cref="ScoreTracker.Tests.DomainTests.RestChartRuleTests" />.
/// </summary>
public sealed class GetRestChartFactsHandlerTests
{
    private readonly List<Chart> _charts = new();
    private readonly List<ChartSkillMetric> _metrics = new();

    private Chart Chart(string name, int level, ChartType type, int seconds, int notes)
    {
        var chart = new Chart(Guid.NewGuid(), MixEnum.Phoenix,
            new Song(Name.From(name), SongType.Arcade, new Uri("https://example.invalid/s.png"),
                TimeSpan.FromSeconds(seconds), Name.From("artist"), null),
            type, DifficultyLevel.From(level), MixEnum.Phoenix, null, notes);
        _charts.Add(chart);
        return chart;
    }

    private void Metrics(Chart chart, double tapRows, double cruxEnps, double hardTwist = 0, double drill = 0)
    {
        _metrics.Add(new ChartSkillMetric(chart.Id, PiuCenterMetrics.TapRows, (decimal)tapRows, null));
        _metrics.Add(new ChartSkillMetric(chart.Id, PiuCenterMetrics.CruxEnps, (decimal)cruxEnps, null));
        _metrics.Add(new ChartSkillMetric(chart.Id, PiuCenterMetrics.BadgeFractionPrefix + "twist_over90",
            (decimal)hardTwist, null));
        _metrics.Add(new ChartSkillMetric(chart.Id, PiuCenterMetrics.BadgeFractionPrefix + "drill",
            (decimal)drill, null));
    }

    private GetRestChartFactsHandler Handler()
    {
        var charts = new Mock<IChartRepository>();
        charts.Setup(c => c.GetCharts(It.IsAny<MixEnum>(), null, null, It.IsAny<IEnumerable<Guid>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((MixEnum _, DifficultyLevel? _, ChartType? _, IEnumerable<Guid>? ids, CancellationToken _) =>
                _charts.Where(c => ids == null || ids.Contains(c.Id)).ToArray());
        var metrics = new Mock<IChartSkillMetricRepository>();
        metrics.Setup(m => m.GetMetrics(It.IsAny<IEnumerable<Guid>>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Guid> ids, string _, CancellationToken _) =>
                _metrics.Where(m => ids.Contains(m.ChartId)).ToArray());
        return new GetRestChartFactsHandler(charts.Object, metrics.Object);
    }

    private Task<IReadOnlyList<Catalog.Contracts.RestChartFacts>> Ask(params Chart[] charts) =>
        Handler().Handle(new GetRestChartFactsQuery(MixEnum.Phoenix, charts.Select(c => c.Id).ToArray()),
            CancellationToken.None);

    /// <summary>Nine busy charts to sit above the one being asked about, so a folder has a shape.</summary>
    private void Folder(int level, ChartType type, int busyCharts = 9)
    {
        for (var i = 0; i < busyCharts; i++)
        {
            var chart = Chart($"Busy {type}{level} {i}", level, type, 120, 1000);
            Metrics(chart, tapRows: 900 + i, cruxEnps: 8 + i);
        }
    }

    [Fact]
    public void ARestfulChartInABusyFolderIsARestChartAndSaysWhy()
    {
        Folder(24, ChartType.Double);
        var slam = Chart("Slam", 24, ChartType.Double, 120, 1000);
        // Few steps, most of the notes held, no drills, a soft crux.
        Metrics(slam, tapRows: 400, cruxEnps: 2);

        var facts = Ask(slam).Result.Single();

        Assert.True(facts.IsRest);
        Assert.True(facts.FewSteps);
        Assert.True(facts.HoldHeavy);
        Assert.True(facts.NoDrills);
        Assert.True(facts.SoftTwists);
        Assert.True(facts.SoftCrux);
        Assert.Equal(0, facts.StepsPercentile);
        Assert.Equal(90, facts.HoldPercentile);
        Assert.Equal(0.6, facts.HoldShare, 3);
        Assert.Equal(400.0 / 120, facts.StepsPerSecond, 3);
    }

    [Fact]
    public void TheSameMeasurementsInAQuieterFolderAreNotRestAtAll()
    {
        // Nine charts every bit as restful: now the candidate is ordinary rather than restful.
        for (var i = 0; i < 9; i++)
        {
            var quiet = Chart($"Quiet {i}", 24, ChartType.Double, 120, 1000);
            Metrics(quiet, tapRows: 380 + i, cruxEnps: 1.5);
        }

        var slam = Chart("Slam", 24, ChartType.Double, 120, 1000);
        Metrics(slam, tapRows: 400, cruxEnps: 2);

        var facts = Ask(slam).Result.Single();

        Assert.False(facts.IsRest);
        Assert.False(facts.FewSteps);
        Assert.False(facts.HoldHeavy);
    }

    [Fact]
    public void AFolderIsMixTypeAndLevelSoNeighbouringLevelsDoNotDiluteIt()
    {
        // A busy D24 folder, and a D23 folder full of restful charts. The D24 candidate must be
        // measured against D24 alone.
        Folder(24, ChartType.Double);
        for (var i = 0; i < 9; i++)
        {
            var easier = Chart($"Easier {i}", 23, ChartType.Double, 120, 1000);
            Metrics(easier, tapRows: 300 + i, cruxEnps: 1);
        }

        var slam = Chart("Slam", 24, ChartType.Double, 120, 1000);
        Metrics(slam, tapRows: 400, cruxEnps: 2);

        Assert.True(Ask(slam).Result.Single().IsRest);
    }

    [Fact]
    public void SinglesAndDoublesAtTheSameLevelAreDifferentFolders()
    {
        Folder(20, ChartType.Single, busyCharts: 9);
        var doublesRest = Chart("Iolite Sky", 20, ChartType.Double, 120, 1000);
        Metrics(doublesRest, tapRows: 400, cruxEnps: 2);

        // Alone in its own folder: no distribution, so nothing can be top-quarter anything.
        Assert.False(Ask(doublesRest).Result.Single().IsRest);
    }

    [Fact]
    public void AChartWithNoStepAnalysisIsAbsentRatherThanReportedAsBusy()
    {
        Folder(24, ChartType.Double);
        var unmeasured = Chart("Unmeasured", 24, ChartType.Double, 120, 1000);

        var facts = Ask(unmeasured).Result;

        Assert.Empty(facts);
    }

    [Fact]
    public void AChartWithNothingToDivideByIsAbsentToo()
    {
        Folder(24, ChartType.Double);
        var noDuration = Chart("No duration", 24, ChartType.Double, 0, 1000);
        Metrics(noDuration, tapRows: 400, cruxEnps: 2);
        var noNotes = Chart("No notes", 24, ChartType.Double, 120, 0);
        Metrics(noNotes, tapRows: 400, cruxEnps: 2);

        Assert.Empty(Ask(noDuration, noNotes).Result);
    }

    [Fact]
    public void AskingAboutNothingReadsNothing()
    {
        Assert.Empty(Handler().Handle(new GetRestChartFactsQuery(MixEnum.Phoenix, Array.Empty<Guid>()),
            CancellationToken.None).Result);
    }
}
