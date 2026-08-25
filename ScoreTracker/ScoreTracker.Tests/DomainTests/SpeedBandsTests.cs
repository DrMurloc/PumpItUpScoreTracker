using System;
using System.Collections.Generic;
using System.Linq;
using ScoreTracker.ChartIntelligence.Domain;
using ScoreTracker.SharedKernel.Enums;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

/// <summary>
///     Speed is folder-relative (docs/design/chart-identity.md §2) — the point of the whole
///     list is that "Fast" means fast for THIS folder.
/// </summary>
public sealed class SpeedBandsTests
{
    private static (Guid, decimal)[] Folder(params decimal[] nps)
    {
        return nps.Select(n => (Guid.NewGuid(), n)).ToArray();
    }

    private static IReadOnlyList<TierListCategory> BandsOf((Guid, decimal)[] folder)
    {
        var entries = SpeedBands.Band(folder).ToDictionary(e => e.ChartId, e => e.Category);
        return folder.Select(c => entries[c.Item1]).ToArray();
    }

    /// <summary>
    ///     The same 12 NPS is the fast end of a slow folder and the slow end of a fast one.
    ///     An absolute cutoff could not do this, and it is why the list is stored per folder.
    /// </summary>
    [Fact]
    public void TheSameSpeedBandsDifferentlyInDifferentFolders()
    {
        var slowFolder = Folder(8, 8, 8.5m, 9, 9, 9.5m, 10, 10, 10, 12);
        var fastFolder = Folder(12, 14, 14, 14.5m, 15, 15, 15, 15.5m, 16, 16);

        var inSlow = BandsOf(slowFolder).Last();
        var inFast = BandsOf(fastFolder).First();

        Assert.True(inSlow > inFast, $"12 NPS read as {inSlow} among slow charts and {inFast} among fast ones");
    }

    [Fact]
    public void BandsRunSlowestToFastestAcrossTheFolder()
    {
        var folder = Folder(6, 8, 9, 10, 10.5m, 11, 11.5m, 12, 13, 15);

        var bands = BandsOf(folder);

        // Sorted input, so the ladder may never step backwards.
        Assert.Equal(bands.OrderBy(b => b).ToArray(), bands.ToArray());
        Assert.True(bands.Distinct().Count() > 1);
    }

    /// <summary>
    ///     A two-chart tail is noise, not a reading — it folds inward rather than stranding
    ///     its charts in a section of two (D24's Very Fast, the case that produced the rule).
    /// </summary>
    [Fact]
    public void AThinTailFoldsIntoItsNeighbourInsteadOfStandingAlone()
    {
        // Twenty charts packed tight, then two far out — the outliers alone in the top band.
        var folder = Folder(Enumerable.Repeat(10m, 10).Concat(Enumerable.Repeat(11m, 10))
            .Concat(new[] { 20m, 21m }).ToArray());

        var bands = BandsOf(folder).ToArray();

        // The outliers share a band with the pack rather than owning one of two — and the
        // bands between them are empty, so folding had to reach past them to find company.
        var outlierBand = bands[^1];
        Assert.True(bands.Count(b => b == outlierBand) >= SpeedBands.MinimumBandSize,
            $"the tail kept {bands.Count(b => b == outlierBand)} charts to itself");
    }

    [Fact]
    public void AFolderWithNoSpreadIsNotBandedAtAll()
    {
        Assert.Empty(SpeedBands.Band(Folder(11, 11, 11, 11, 11)));
    }

    [Fact]
    public void AFolderTooSmallToHaveADistributionIsNotBanded()
    {
        Assert.Empty(SpeedBands.Band(Folder(11)));
        Assert.Empty(SpeedBands.Band(Array.Empty<(Guid, decimal)>()));
    }

    /// <summary>
    ///     The measurement rides in Order so a reader can print the real NPS beside the band
    ///     name without going back for the metric.
    /// </summary>
    [Fact]
    public void TheOrderCarriesTheMeasuredNpsSoTheNumberSurvives()
    {
        var folder = Folder(6.5m, 9, 11, 12, 13.25m, 15);

        var entries = SpeedBands.Band(folder).ToDictionary(e => e.ChartId);

        Assert.Equal(650, entries[folder[0].Item1].Order);
        Assert.Equal(1325, entries[folder[4].Item1].Order);
    }

    [Fact]
    public void EveryEntryIsWrittenUnderTheSpeedListName()
    {
        var entries = SpeedBands.Band(Folder(8, 10, 12, 14));

        Assert.All(entries, e => Assert.Equal("Speed", e.TierListName));
    }
}
