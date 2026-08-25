using System;
using System.Collections.Generic;
using System.Linq;
using ScoreTracker.Catalog.Contracts;
using ScoreTracker.Catalog.Domain;
using ScoreTracker.SharedKernel.Enums;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

/// <summary>
///     The chip rules, and the golden examples from docs/design/chart-identity.md §8 — the ones
///     the owner validated by eye against real folders. These are the acceptance bar: an
///     implementation that stops reproducing them is wrong, not differently tuned.
/// </summary>
public sealed class ChartIdentityBuilderTests
{
    /// <summary>
    ///     A folder built from charts described as (badge, coverage) pairs, so a test states
    ///     the company a chart keeps rather than hand-computing cutoffs.
    /// </summary>
    private sealed class Folder
    {
        private readonly List<ChartBadgeProfile> _charts = new();

        public Folder AddCharts(int count, params (string Badge, decimal Coverage)[] coverage)
        {
            for (var i = 0; i < count; i++) _charts.Add(Profile(coverage));
            return this;
        }

        public static ChartBadgeProfile Profile((string Badge, decimal Coverage)[] coverage,
            IReadOnlyDictionary<string, int>? dominance = null, decimal? peakiness = null,
            IReadOnlyList<string>? cruxBadges = null)
        {
            return new ChartBadgeProfile(Guid.NewGuid(),
                coverage.ToDictionary(c => c.Badge, c => c.Coverage, StringComparer.OrdinalIgnoreCase),
                dominance ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
                peakiness, cruxBadges ?? Array.Empty<string>());
        }

        public IReadOnlyList<IdentityChipRecord> ChipsFor(ChartBadgeProfile subject)
        {
            var all = _charts.Append(subject).ToArray();
            var baselines = FolderBaselineBuilder.Build(MixEnum.Phoenix, ChartType.Double, 24, all)
                .ToDictionary(b => b.Badge, b => b, StringComparer.OrdinalIgnoreCase);
            return ChartIdentityBuilder.Build(subject, baselines);
        }
    }

    private static IEnumerable<string> Badges(IReadOnlyList<IdentityChipRecord> chips, IdentityChipKind kind)
    {
        return chips.Where(c => c.Kind == kind).Select(c => c.Badge);
    }

    /// <summary>
    ///     §8, Achluoias D24: a run chart whose #3 dominance pick is bracket_drill on 12.5%
    ///     measured brackets. The pick renders nowhere near a Brackets claim — this is the rule
    ///     the owner caught by spotting the chart in a Fast Brackets section.
    /// </summary>
    [Fact]
    public void ADominanceOnlyPickWithThinCoverageNeverBecomesPresence()
    {
        var folder = new Folder().AddCharts(20, ("bracket", 0.45m), ("mid6_doubles", 0.5m));
        var achluoias = Folder.Profile(
            new[] { ("anchor_run", 0.375m), ("drill", 0.375m), ("bracket", 0.125m) },
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                { ["anchor_run"] = 1, ["drill"] = 2, ["bracket_drill"] = 3 });

        var chips = folder.ChipsFor(achluoias);

        Assert.Contains("anchor_run", Badges(chips, IdentityChipKind.Unique));
        Assert.Contains("drill", Badges(chips, IdentityChipKind.Unique));
        Assert.DoesNotContain(chips, c => c.Badge is "bracket" or "bracket_drill");
    }

    /// <summary>§8, Scorpion King D23: a spike big enough to name, made of something new.</summary>
    [Fact]
    public void ASpikeShowsItsCruxWhenTheCruxSaysSomethingTheOtherChipsDidNot()
    {
        var folder = new Folder().AddCharts(20, ("run", 0.5m));
        var scorpionKing = Folder.Profile(
            new[] { ("bracket", 0.4m), ("doublestep", 0.35m) },
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["bracket"] = 1, ["bursty"] = 2 },
            1.5m, new[] { "bracket_jump", "bracket" });

        var chips = folder.ChipsFor(scorpionKing);

        var spike = Assert.Single(chips.Where(c => c.Kind == IdentityChipKind.Spike));
        Assert.Equal(1.5m, spike.Detail);
        Assert.Empty(spike.Badge);
        // bracket_jump is new; bracket already rode a chip above, so it is not repeated.
        Assert.Equal(new[] { "bracket_jump" }, Badges(chips, IdentityChipKind.Crux));
    }

    /// <summary>
    ///     §8, 8 6 FULL SONG D23: peakiness of −1.0. No passage reaches the printed level —
    ///     the difficulty is the eighty-second grind, and claiming a spike would be a lie.
    /// </summary>
    [Fact]
    public void AChartWhoseHardestStretchUndershootsItsPrintedLevelGetsNoSpike()
    {
        var folder = new Folder().AddCharts(20, ("run", 0.5m));
        var grind = Folder.Profile(new[] { ("doublestep", 0.5m), ("mid4_doubles", 0.45m) },
            peakiness: -1.0m, cruxBadges: new[] { "mid4_doubles" });

        var chips = folder.ChipsFor(grind);

        Assert.DoesNotContain(chips, c => c.Kind is IdentityChipKind.Spike or IdentityChipKind.Crux);
    }

    /// <summary>
    ///     §8, Uranium D24 and Cygnus D23: thin coverage everywhere. Rather than invent a
    ///     distinction, the chart says what piucenter said — muted, and in their order.
    /// </summary>
    [Fact]
    public void AChartThatStandsOutNowhereFallsBackToPiucentersOwnPicks()
    {
        var folder = new Folder().AddCharts(20, ("mid6_doubles", 0.6m), ("run", 0.5m));
        var uranium = Folder.Profile(new[] { ("mid6_doubles", 0.1m) },
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                { ["jack"] = 1, ["twist_far"] = 2, ["10-stair"] = 3 });

        var chips = folder.ChipsFor(uranium);

        Assert.Equal(new[] { "jack", "twist_far", "10-stair" }, Badges(chips, IdentityChipKind.Fallback));
        Assert.All(chips, c => Assert.Equal(IdentityChipKind.Fallback, c.Kind));
    }

    /// <summary>
    ///     The ✦ rule at the bottom of the ladder: at low levels a bracket existing at all is
    ///     the chart's whole identity, and the rule fires on its own the first folder one shows
    ///     up in — no per-level table anywhere.
    /// </summary>
    [Fact]
    public void TheFirstChartInAFolderToCarryABadgeAtAllIsMarkedUnique()
    {
        var folder = new Folder().AddCharts(30, ("jack", 0.5m), ("doublestep", 0.4m));
        var oddOneOut = Folder.Profile(new[] { ("bracket", 0.4m), ("jack", 0.5m) });

        var chips = folder.ChipsFor(oddOneOut);

        Assert.Equal(new[] { "bracket" }, Badges(chips, IdentityChipKind.Unique));
        Assert.DoesNotContain("jack", Badges(chips, IdentityChipKind.Unique));
    }

    [Fact]
    public void ChipsAreCappedSoACardStaysReadable()
    {
        var folder = new Folder().AddCharts(20, ("run", 0.1m));
        var everything = Folder.Profile(new[]
            {
                ("bracket", 0.9m), ("twist_90", 0.9m), ("drill", 0.9m), ("jack", 0.9m), ("split", 0.9m),
                ("yog_walk", 0.9m), ("hands", 0.9m)
            },
            peakiness: 1.2m, cruxBadges: new[] { "10-stair", "mid4_doubles", "co-op_pad_transition" });

        var chips = folder.ChipsFor(everything);

        Assert.Equal(ChartIdentityRules.MaxUniqueChips, Badges(chips, IdentityChipKind.Unique).Count());
        Assert.Equal(ChartIdentityRules.MaxCoreChips, Badges(chips, IdentityChipKind.Core).Count());
        Assert.Equal(ChartIdentityRules.MaxCruxChips, Badges(chips, IdentityChipKind.Crux).Count());
    }

    /// <summary>
    ///     Whole-chart qualities carry no coverage, so they never claim one: the chip shows the
    ///     quality and nothing else, where a measured badge carries its number.
    /// </summary>
    [Fact]
    public void WholeChartQualitiesRenderWithoutACoverageNumber()
    {
        var folder = new Folder().AddCharts(20, ("run", 0.5m));
        var sustained = Folder.Profile(new[] { ("run", 0.8m) },
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["sustained"] = 1 });

        var chips = folder.ChipsFor(sustained);

        var sustainedChip = Assert.Single(chips.Where(c => c.Badge == "sustained"));
        Assert.Null(sustainedChip.Detail);
        Assert.Equal(0.8m, chips.Single(c => c.Badge == "run").Detail);
    }

    [Fact]
    public void EveryChipCarriesItsBadgeFamilyExceptTheSpike()
    {
        var folder = new Folder().AddCharts(20, ("run", 0.5m));
        var chart = Folder.Profile(new[] { ("bracket", 0.6m) }, peakiness: 1.0m, cruxBadges: new[] { "twist_90" });

        var chips = folder.ChipsFor(chart);

        Assert.Equal(BadgeCategory.Brackets, chips.Single(c => c.Badge == "bracket").Family);
        Assert.Equal(BadgeCategory.Twists, chips.Single(c => c.Badge == "twist_90").Family);
        Assert.Null(chips.Single(c => c.Kind == IdentityChipKind.Spike).Family);
    }
}
