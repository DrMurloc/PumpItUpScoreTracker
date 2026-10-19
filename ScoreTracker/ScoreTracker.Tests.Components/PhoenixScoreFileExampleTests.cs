using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Localization;
using Moq;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Services;
using Xunit;
using ChartType = ScoreTracker.SharedKernel.Enums.ChartType;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The example spreadsheet the import page hands out (docs/design/import-scores-refresh.md, amended
///     2026-10-18): which charts its rows land on, and the mix's own words in its cells. That the whole file
///     uploads is the page suite's TheExampleUploadsAsItIs.
/// </summary>
public sealed class PhoenixScoreFileExampleTests
{
    private static readonly IStringLocalizer Localizer = PassThroughLocalizer();

    [Fact]
    public void ARowWhoseSongTheMixLacksTakesTheClosestChartOfItsType()
    {
        // No Switronic here: the stage-break row lands on the Doubles chart nearest its level, and not on the
        // Final Audition 2 chart the row before it already took.
        var catalog = new[]
        {
            CatalogChart(MixEnum.Phoenix2, "BRAIN POWER", ChartType.Single, 16),
            CatalogChart(MixEnum.Phoenix2, "Conflict", ChartType.Single, 17),
            CatalogChart(MixEnum.Phoenix2, "Chinese restaurant", ChartType.Single, 18),
            CatalogChart(MixEnum.Phoenix2, "Final Audition 2", ChartType.Double, 20),
            CatalogChart(MixEnum.Phoenix2, "Bee", ChartType.Double, 24),
            CatalogChart(MixEnum.Phoenix2, "Gargoyle - FULL SONG -", ChartType.Double, 21)
        };

        var rows = Cells(PhoenixScoreFileExample.Rows(MixEnum.Phoenix2, catalog, Localizer));

        Assert.Equal(5, rows.Count);
        Assert.Equal("Gargoyle - FULL SONG -", rows[4]["Song"]);
        Assert.Equal("D21", rows[4]["Difficulty"]);
        Assert.Equal("TRUE", rows[4]["IsBroken"]);
    }

    [Fact]
    public void NoTwoRowsShareAChart()
    {
        // Two charts cannot fill five rows; a repeated chart would only overwrite itself on the way back in,
        // so the rows that find nothing left are dropped.
        var catalog = new[]
        {
            CatalogChart(MixEnum.Phoenix2, "Conflict", ChartType.Single, 17),
            CatalogChart(MixEnum.Phoenix2, "Switronic", ChartType.Double, 22)
        };

        var rows = Cells(PhoenixScoreFileExample.Rows(MixEnum.Phoenix2, catalog, Localizer));

        Assert.Equal(2, rows.Count);
        Assert.Equal(2, rows.Select(r => $"{r["Song"]} {r["Difficulty"]}").Distinct().Count());
    }

    [Fact]
    public void ARiseExampleWritesItsThreeMarksAClearWithNoMarkAndHalfDoubles()
    {
        var catalog = new[]
        {
            CatalogChart(MixEnum.Rise, "BRAIN POWER", ChartType.Single, 16),
            CatalogChart(MixEnum.Rise, "Conflict", ChartType.Single, 17),
            CatalogChart(MixEnum.Rise, "Chinese restaurant", ChartType.Single, 18),
            CatalogChart(MixEnum.Rise, "Final Audition 2", ChartType.HalfDouble, 20),
            CatalogChart(MixEnum.Rise, "Switronic", ChartType.HalfDouble, 18)
        };

        var rows = Cells(PhoenixScoreFileExample.Rows(MixEnum.Rise, catalog, Localizer));

        Assert.Equal(new[] { "Song", "Difficulty", "Score", "Plate", "IsBroken", "Notes (not imported)" },
            rows[0].Keys);
        Assert.Equal(new[] { "S16", "S17", "S18", "HD20", "HD18" }, rows.Select(r => (string?)r["Difficulty"]));
        Assert.Equal(new[] { "PG", "FC", "NM", "", "" }, rows.Select(r => (string?)r["Plate"]));
        Assert.Equal(new[] { "FALSE", "FALSE", "FALSE", "FALSE", "TRUE" }, rows.Select(r => (string?)r["IsBroken"]));
        Assert.Equal("1000000", rows[0]["Score"]);
    }

    [Theory]
    [InlineData(MixEnum.Phoenix, "piu-scores-example-phoenix.csv")]
    [InlineData(MixEnum.Phoenix2, "piu-scores-example-phoenix-2.csv")]
    [InlineData(MixEnum.Rise, "piu-scores-example-rise.csv")]
    [InlineData(MixEnum.RiseArcade, "piu-scores-example-rise-arcade.csv")]
    public void TheFileIsNamedForItsMix(MixEnum mix, string fileName)
    {
        Assert.Equal(fileName, PhoenixScoreFileExample.FileName(mix));
    }

    private static List<IDictionary<string, object?>> Cells(IEnumerable<dynamic> rows)
    {
        return rows.Select(r => (IDictionary<string, object?>)r).ToList();
    }

    private static Chart CatalogChart(MixEnum mix, string song, ChartType type, int level)
    {
        return new Chart(Guid.NewGuid(), mix,
            new Song(song, SongType.Arcade, new Uri("https://piu.test/art.png"),
                TimeSpan.FromMinutes(2), "Artist", Bpm.From(150, 150)),
            type, level, mix, null, 1200);
    }

    private static IStringLocalizer PassThroughLocalizer()
    {
        var localizer = new Mock<IStringLocalizer>();
        localizer.Setup(l => l[It.IsAny<string>()])
            .Returns((string key) => new LocalizedString(key, key));
        localizer.Setup(l => l[It.IsAny<string>(), It.IsAny<object[]>()])
            .Returns((string key, object[] args) => new LocalizedString(key, string.Format(key, args)));
        return localizer.Object;
    }
}
