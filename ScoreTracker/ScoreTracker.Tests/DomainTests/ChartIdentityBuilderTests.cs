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
///     the owner validated against real folders, chart by chart. These are the acceptance bar:
///     an implementation that stops reproducing them is wrong, not differently tuned.
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

        /// <summary>
        ///     A folder of twenty identical charts makes every percentile the same number, so
        ///     p10 and p90 collide and every geometry claim fires at once. Real folders spread;
        ///     this fans the values out so a subject sitting mid-range claims nothing.
        /// </summary>
        public Folder AddCharts(int count, IReadOnlyDictionary<string, decimal> geometry,
            params (string Badge, decimal Coverage)[] coverage)
        {
            for (var i = 0; i < count; i++)
            {
                var spread = (decimal)i / (count * 5);
                _charts.Add(Profile(coverage, geometry: geometry.ToDictionary(
                    kv => kv.Key, kv => kv.Value + spread * kv.Value, StringComparer.OrdinalIgnoreCase)));
            }

            return this;
        }

        public static ChartBadgeProfile Profile((string Badge, decimal Coverage)[] coverage,
            IReadOnlyDictionary<string, int>? dominance = null, decimal? peakiness = null,
            IReadOnlyList<string>? cruxBadges = null, decimal? cruxDuration = null,
            IReadOnlyDictionary<string, decimal>? geometry = null)
        {
            return new ChartBadgeProfile(Guid.NewGuid(),
                coverage.ToDictionary(c => c.Badge, c => c.Coverage, StringComparer.OrdinalIgnoreCase),
                dominance ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
                peakiness, cruxBadges ?? Array.Empty<string>(), cruxDuration, geometry);
        }

        /// <summary>The folder's own outer Speed boundary, as the baseline builder computes it.</summary>
        public decimal SpeedBound(IReadOnlyDictionary<string, decimal> probe, bool fast)
        {
            var subject = Profile(Array.Empty<(string, decimal)>(), geometry: probe);
            var baseline = FolderBaselineBuilder.Build(MixEnum.Phoenix, ChartType.Double, 24,
                    _charts.Append(subject).ToArray())
                .Single(b => b.Badge == PiuCenterMetrics.Nps);
            return fast ? baseline.DrenchedCutoff : baseline.CoreCutoff;
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

    private static IEnumerable<string> IdentityBadges(IReadOnlyList<IdentityChipRecord> chips)
    {
        return chips.Where(c => c.Tier == IdentityTier.Identity).Select(c => c.Badge);
    }

    private static IReadOnlyDictionary<string, decimal> Geometry(decimal mid6 = 0.92m, decimal sideOn = 0.22m,
        decimal crossed = 0.05m, decimal brackets = 0.06m, decimal mid4 = 0.70m, decimal tension = 30m,
        decimal repeated = 0.20m, decimal longestRun = 0m, decimal span = 100m)
    {
        return new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            [PiuCenterMetrics.PadShareMid6] = mid6,
            [PiuCenterMetrics.PadShareMid4] = mid4,
            [PiuCenterMetrics.StanceSideOn] = sideOn,
            [PiuCenterMetrics.StanceCrossed] = crossed,
            [PiuCenterMetrics.BracketRowShare] = brackets,
            [PiuCenterMetrics.TimeUnderTension] = tension,
            [PiuCenterMetrics.RepeatedPanelShare] = repeated,
            [PiuCenterMetrics.SustainTime] = longestRun,
            [PiuCenterMetrics.ChartSpan] = span
        };
    }

    /// <summary>
    ///     §3.6. Piucenter defines a footswitch as a repeated single panel where the PREDICTED
    ///     limbs differ, so a footswitch and a jack are the same note pattern and only an ML guess
    ///     separates them. The pattern has to exist before a reading of it can: Baroque Virus FULL
    ///     S21 was called a hold-footslide chart on three repeated-panel rows in 2,327, while
    ///     Headless Chicken S21 (15.8%) and Hi-Bi D21 (16.8%) are the real ones.
    /// </summary>
    [Fact]
    public void ALimbPredictedBadgeNeedsTheNotePatternItIsMadeOf()
    {
        var folder = new Folder().AddCharts(20, Geometry(), ("run", 0.5m));
        var picks = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["footswitch"] = 1 };
        var headless = Folder.Profile(new[] { ("footswitch", 0.4545m) }, picks,
            geometry: Geometry(repeated: 0.158m));
        var phantom = Folder.Profile(new[] { ("footswitch", 0.4545m) }, picks,
            geometry: Geometry(repeated: 0.017m));

        Assert.Contains("footswitch", IdentityBadges(folder.ChipsFor(headless)));
        // Same coverage, same pick — and no repeated panels to have switched feet on.
        Assert.DoesNotContain("footswitch", IdentityBadges(folder.ChipsFor(phantom)));
        Assert.DoesNotContain(folder.ChipsFor(phantom), c => c.Badge == "footswitch");
    }

    /// <summary>
    ///     §3.8. Their "Sustain time" is max(length) over the eNPS ranges of interest — the chart's
    ///     LONGEST single run, not a total — so it can be named as one. Absolute rather than
    ///     folder-relative (owner): a fifty-second run is a fifty-second run whoever it stands
    ///     next to.
    /// </summary>
    [Theory]
    [InlineData(46.5, true)]
    [InlineData(22.5, true)]
    [InlineData(14.3, false)]
    [InlineData(11.2, false)]
    public void TheLongestRunIsAClaimWhenItIsMostOfTheChart(double sharePercent, bool expected)
    {
        var folder = new Folder().AddCharts(20, Geometry(), ("run", 0.5m));
        var chart = Folder.Profile(Array.Empty<(string, decimal)>(),
            geometry: Geometry(longestRun: (decimal)sharePercent, span: 100m));

        var chips = folder.ChipsFor(chart);
        var run = chips.SingleOrDefault(c => c.Kind == IdentityChipKind.LongestRun);

        Assert.Equal(expected, run != null);
        if (expected) Assert.Equal((decimal)sharePercent, run!.Detail);
    }

    /// <summary>
    ///     §3.1 and the union. A dominance pick under the bar establishes no COVERAGE claim —
    ///     that is the Achluoias rule — but the pick itself is now carried as piucenter's own
    ///     opinion (owner, 2026-08-26: "Union it"). The vetoes still apply to picks, which is what
    ///     keeps a bracket pick off a chart that does not bracket.
    /// </summary>
    [Fact]
    public void ADominanceOnlyPickIsCarriedAsTheirOpinionButNeverAsOurMeasurement()
    {
        var folder = new Folder().AddCharts(20, Geometry(), ("bracket", 0.45m), ("mid6_doubles", 0.5m));
        var picks = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            { ["anchor_run"] = 1, ["drill"] = 2, ["bracket_drill"] = 3 };
        var achluoias = Folder.Profile(
            new[] { ("anchor_run", 0.375m), ("drill", 0.375m), ("bracket", 0.125m) },
            picks, geometry: Geometry());
        var barelyBrackets = Folder.Profile(
            new[] { ("anchor_run", 0.375m), ("drill", 0.375m), ("bracket", 0.125m) },
            picks, geometry: Geometry(brackets: 0.004m));

        var chips = folder.ChipsFor(achluoias);

        // Our own claims come from coverage, and 12.5% brackets is not one.
        Assert.DoesNotContain(chips, c => c.Badge == "bracket");
        Assert.Contains("anchor_run", Badges(chips, IdentityChipKind.Unique));
        // Their bracket_drill pick rides along, because this chart really does bracket.
        Assert.Contains("bracket_drill", IdentityBadges(chips));
        // On a chart that does not, the veto takes it straight back out.
        Assert.DoesNotContain("bracket_drill", IdentityBadges(folder.ChipsFor(barelyBrackets)));
    }

    /// <summary>
    ///     §3.2, Nakakapagpabagabag D20 — the bug the owner caught by naming four charts that
    ///     are "entirely double steps" and highlighting none of them. Drenched was twice the
    ///     folder's 75th percentile, and twice a p75 routinely sits ABOVE the folder's own
    ///     maximum: here the folder tops out at .714 and the old rule demanded .727, so the
    ///     chart holding the folder record still failed. A percentile always exists.
    /// </summary>
    [Fact]
    public void TheChartWithTheMostOfABadgeInItsFolderClaimsIt()
    {
        // A folder whose doublestep coverages top out at exactly the subject's own value.
        var folder = new Folder()
            .AddCharts(14, ("doublestep", 0.30m))
            .AddCharts(5, ("doublestep", 0.50m));
        var nakaka = Folder.Profile(new[] { ("doublestep", 0.7143m) });

        var chips = folder.ChipsFor(nakaka);

        Assert.Contains("doublestep", IdentityBadges(chips));
    }

    /// <summary>
    ///     §3.2, That Kitty D22 again, now under the folder-relative bar. Jacks are on 59% of
    ///     that folder, so the budget makes them expensive to claim — a chart needs to be one of
    ///     the few that really jack, and three scattered jack segments is not that. The earlier
    ///     version of this test used a folder where jacks were rare, which is not the folder That
    ///     Kitty is actually in.
    /// </summary>
    [Fact]
    public void ACommonTechniqueTakesMoreThanAFewSegmentsToClaim()
    {
        var folder = new Folder()
            .AddCharts(1, ("jack", 0.50m)).AddCharts(1, ("jack", 0.48m))
            .AddCharts(1, ("jack", 0.46m)).AddCharts(1, ("jack", 0.44m))
            .AddCharts(9, ("jack", 0.15m))
            .AddCharts(8, ("mid6_doubles", 0.5m));
        var thatKitty = Folder.Profile(new[] { ("jack", 0.4286m) });

        var chips = folder.ChipsFor(thatKitty);

        Assert.DoesNotContain("jack", IdentityBadges(chips));
    }

    /// <summary>
    ///     §3.4, Heliosphere D20: eleven bracket rows in 845. Piucenter's bracket detection is a
    ///     limb-assignment model that reads ordinary jumps as brackets, and it clustered five of
    ///     them in the final section — which is why the chart wore a bracket-jump badge nobody
    ///     watching it could find. The veto reaches the hard-section chip too, which is the only
    ///     door that badge had left.
    /// </summary>
    [Fact]
    public void BracketBadgesAreVetoedOnAChartThatBarelyBrackets()
    {
        var folder = new Folder().AddCharts(20, Geometry(), ("run", 0.5m));
        var heliosphere = Folder.Profile(new[] { ("bracket_jump", 0.45m), ("drill", 0.4444m) },
            peakiness: 1.02m, cruxBadges: new[] { "run", "bracket_jump", "drill" }, cruxDuration: 14.4m,
            geometry: Geometry(brackets: 0.013m));

        var chips = folder.ChipsFor(heliosphere);

        Assert.DoesNotContain(chips, c => c.Badge == "bracket_jump");
        var section = Assert.Single(chips.Where(c => c.Kind == IdentityChipKind.HardSection));
        Assert.DoesNotContain(section.Badges!, b => b.Badge == "bracket_jump");
        // drill claimed the chart above (nothing else in the folder drills), and a badge
        // already claimed is not news again in the hard-section chip.
        Assert.Contains("drill", IdentityBadges(chips));
        Assert.Equal(new[] { "run" }, section.Badges!.Select(b => b.Badge));
    }

    /// <summary>
    ///     §3.3. One window, so one chip: a second would print the same duration again. The
    ///     owner's line for BSPower was "Hardest 10s: Drills 90 degree twists", which is this.
    /// </summary>
    [Fact]
    public void TheHardestStretchIsOneChipCarryingItsLengthAndItsBadges()
    {
        var folder = new Folder().AddCharts(20, Geometry(), ("run", 0.5m));
        var bsPower = Folder.Profile(Array.Empty<(string, decimal)>(),
            peakiness: 0.62m, cruxBadges: new[] { "drill", "twist_90", "mid4_doubles" }, cruxDuration: 9.75m,
            geometry: Geometry());

        var chips = folder.ChipsFor(bsPower);

        var section = Assert.Single(chips.Where(c => c.Kind == IdentityChipKind.HardSection));
        Assert.Equal(9.75m, section.Detail);
        // Pad geography is the width chip's business — left in, Burn Out's crux would
        // resurrect the mid-4 chip the owner rejected on that chart.
        Assert.Equal(new[] { "drill", "twist_90" }, section.Badges!.Select(b => b.Badge));
        Assert.Equal(IdentityTier.Identity, section.Tier);
    }

    /// <summary>
    ///     §3.3. Three gates, because elevation and composition are different questions.
    ///     Calibrated on the owner's own reports: That Kitty at .17 stays silent, New Rose at
    ///     .29 speaks as a feature, BSPower at .62 headlines, and only .7 earns the spike.
    /// </summary>
    [Theory]
    [InlineData(0.17, false, null)]
    [InlineData(0.29, false, IdentityTier.Feature)]
    [InlineData(0.62, false, IdentityTier.Identity)]
    [InlineData(0.80, true, IdentityTier.Identity)]
    public void AHardStretchIsNamedLongBeforeItIsCalledASpike(double peakiness, bool expectSpike,
        IdentityTier? expectedTier)
    {
        var folder = new Folder().AddCharts(20, Geometry(), ("run", 0.5m));
        var chart = Folder.Profile(Array.Empty<(string, decimal)>(),
            peakiness: (decimal)peakiness, cruxBadges: new[] { "run" }, cruxDuration: 15m,
            geometry: Geometry());

        var chips = folder.ChipsFor(chart);

        Assert.Equal(expectSpike, chips.Any(c => c.Kind == IdentityChipKind.Spike));
        var section = chips.SingleOrDefault(c => c.Kind == IdentityChipKind.HardSection);
        Assert.Equal(expectedTier, section?.Tier);
    }

    /// <summary>
    ///     §3.2. A chart is charted WITHIN the middle six or it is not, so this end of the width
    ///     axis is absolute rather than folder-relative. Hymn of Golden Glory SC D20 measures
    ///     99.48% — it steps outside twice, and twice is not never.
    /// </summary>
    [Theory]
    [InlineData(1.0, true)]
    [InlineData(0.9948, false)]
    public void HalfDoubleMeansTheChartNeverLeavesTheMiddleSix(double mid6, bool expected)
    {
        var folder = new Folder().AddCharts(20, Geometry(), ("run", 0.5m));
        var chart = Folder.Profile(Array.Empty<(string, decimal)>(),
            geometry: Geometry(mid6: (decimal)mid6, mid4: 0.79m));

        var chips = folder.ChipsFor(chart);

        Assert.Equal(expected, chips.Any(c => c.Badge == IdentityClaimKeys.HalfDouble));
    }

    /// <summary>
    ///     §4b, Vook D20: 8.8% side-on stances, of which 7.8% are crossovers. A chart that
    ///     barely rotates but crosses your feet hard the few times it moves is not twistless,
    ///     and calling it that would be the measure lying about its one job.
    /// </summary>
    [Fact]
    public void AChartThatCrossesHardIsNotTwistlessNoMatterHowRarelyItTurns()
    {
        var folder = new Folder().AddCharts(20, Geometry(), ("run", 0.5m));
        var vook = Folder.Profile(Array.Empty<(string, decimal)>(),
            geometry: Geometry(sideOn: 0.088m, crossed: 0.078m));
        var jupin = Folder.Profile(Array.Empty<(string, decimal)>(),
            geometry: Geometry(sideOn: 0.0m, crossed: 0.0m));

        Assert.DoesNotContain(IdentityClaimKeys.Twistless, folder.ChipsFor(vook).Select(c => c.Badge));
        Assert.Contains(IdentityClaimKeys.Twistless, folder.ChipsFor(jupin).Select(c => c.Badge));
    }

    /// <summary>
    ///     §2. Speed claims a chart only in the outer bands, and the boundary is not softened to
    ///     catch near misses — A Site De La Rue D20 sits at z = 1.46 and gets nothing. The test
    ///     reads the folder's own computed boundary rather than a guessed number, so it stays
    ///     true if the constant moves.
    /// </summary>
    [Fact]
    public void SpeedOnlyClaimsAChartAtTheExtremes()
    {
        var folder = new Folder();
        for (var i = 0; i < 20; i++) folder.AddCharts(1, Speeds(9.5m + i * 0.1m), ("run", 0.5m));

        var fastBound = folder.SpeedBound(Speeds(11m), true);
        Assert.Equal(IdentityClaimKeys.VeryFast, SpeedClaim(folder, fastBound + 0.2m));
        Assert.Null(SpeedClaim(folder, fastBound - 0.2m));

        var slowBound = folder.SpeedBound(Speeds(11m), false);
        Assert.Equal(IdentityClaimKeys.VerySlow, SpeedClaim(folder, slowBound - 0.2m));
        Assert.Null(SpeedClaim(folder, slowBound + 0.2m));
    }

    private static string? SpeedClaim(Folder folder, decimal nps)
    {
        var chart = Folder.Profile(Array.Empty<(string, decimal)>(), geometry: Speeds(nps));
        return folder.ChipsFor(chart).FirstOrDefault(c => c.Kind == IdentityChipKind.Speed)?.Badge;
    }

    private static IReadOnlyDictionary<string, decimal> Speeds(decimal nps)
    {
        var geometry = Geometry().ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);
        geometry[PiuCenterMetrics.Nps] = nps;
        return geometry;
    }

    /// <summary>
    ///     §3.2. A sustained pick is cheap — Monolith D23 carries one over ten seconds of
    ///     tension, which is nobody's idea of a grind. The claim needs the folder to agree the
    ///     chart is actually long.
    /// </summary>
    [Fact]
    public void SustainedNeedsTheFolderToAgreeTheChartIsLong()
    {
        var folder = new Folder().AddCharts(20, Geometry(tension: 30m), ("run", 0.5m));
        var pick = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["sustained"] = 1 };
        var monolith = Folder.Profile(Array.Empty<(string, decimal)>(), pick, geometry: Geometry(tension: 10m));
        var gargoyle = Folder.Profile(Array.Empty<(string, decimal)>(), pick, geometry: Geometry(tension: 141m));

        Assert.DoesNotContain("sustained", IdentityBadges(folder.ChipsFor(monolith)));
        Assert.Contains("sustained", IdentityBadges(folder.ChipsFor(gargoyle)));
    }

    /// <summary>
    ///     §3.9. Hold share claims a chart only at the folder's outer deciles — the middle is a
    ///     measurement, not a claim, exactly like the Speed bands.
    /// </summary>
    [Fact]
    public void HoldShareClaimsAChartOnlyAtTheOuterDeciles()
    {
        var folder = new Folder();
        for (var i = 0; i < 20; i++) folder.AddCharts(1, Holds(0.30m + i * 0.01m), ("run", 0.5m));

        Assert.Equal(IdentityClaimKeys.HoldHeavy, HoldClaim(folder, 0.70m));
        Assert.Equal(IdentityClaimKeys.FewHolds, HoldClaim(folder, 0.10m));
        Assert.Null(HoldClaim(folder, 0.40m));
    }

    /// <summary>
    ///     §3.9. Below S10 most of a folder has no holds at all, so its p10 IS zero — an
    ///     unfloored low claim fires on a third of S01–S03. The absence of holds only says
    ///     something where holds are what is normal.
    /// </summary>
    [Fact]
    public void FewHoldsSaysNothingWhereHoldsAreNotTheNorm()
    {
        var folder = new Folder();
        for (var i = 0; i < 18; i++) folder.AddCharts(1, Holds(0m), ("run", 0.5m));
        folder.AddCharts(1, Holds(0.40m), ("run", 0.5m));
        folder.AddCharts(1, Holds(0.45m), ("run", 0.5m));

        Assert.Null(HoldClaim(folder, 0m));
    }

    /// <summary>
    ///     §3.9. In a thin window the p90 cutoff IS the top value or two, so the extremes wear
    ///     these chips by rank alone — of D28's eleven boss charts, the holdiest two and the
    ///     least would all have claimed something. Below fifteen measured peers the hold claims
    ///     say nothing in either direction (owner, 2026-08-30), and the floor covers any boss
    ///     folder that appears later the day it exists.
    /// </summary>
    [Fact]
    public void HoldClaimsNeedFifteenMeasuredPeers()
    {
        var thin = new Folder();
        for (var i = 0; i < 13; i++) thin.AddCharts(1, Holds(0.30m + i * 0.01m), ("run", 0.5m));
        Assert.Null(HoldClaim(thin, 0.70m));
        Assert.Null(HoldClaim(thin, 0.10m));

        var enough = new Folder();
        for (var i = 0; i < 14; i++) enough.AddCharts(1, Holds(0.30m + i * 0.01m), ("run", 0.5m));
        Assert.Equal(IdentityClaimKeys.HoldHeavy, HoldClaim(enough, 0.70m));
    }

    /// <summary>
    ///     §3.9. The inference borrows the file's step count, and a file that is not the shipped
    ///     chart always errs by inflating the holds — so a high share from a disbelieved file is
    ///     SILENCE, not a fall-through: a chart at the top of its folder is not "few holds"
    ///     either way. Destination SHORT CUT D20 is the calibration case.
    /// </summary>
    [Fact]
    public void AHighHoldClaimNeedsAFileThatCanAccountForIt()
    {
        var folder = new Folder();
        for (var i = 0; i < 20; i++) folder.AddCharts(1, Holds(0.30m + i * 0.01m), ("run", 0.5m));
        var destination = Folder.Profile(Array.Empty<(string, decimal)>(), geometry: Holds(0.65m))
            with { HoldsAreCredible = false };

        Assert.DoesNotContain(folder.ChipsFor(destination), c => c.Kind == IdentityChipKind.Holds);
    }

    private static string? HoldClaim(Folder folder, decimal share)
    {
        var chart = Folder.Profile(Array.Empty<(string, decimal)>(), geometry: Holds(share));
        return folder.ChipsFor(chart).FirstOrDefault(c => c.Kind == IdentityChipKind.Holds)?.Badge;
    }

    private static IReadOnlyDictionary<string, decimal> Holds(decimal share)
    {
        var geometry = Geometry().ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);
        geometry[PiuCenterMetrics.HoldShare] = share;
        return geometry;
    }

    /// <summary>
    ///     §8, revised 2026-08-29. That Kitty D22 was the third "earns nothing" golden row — it
    ///     pinned the over-claiming rules its jacks exposed — and it gained Hold-heavy when the
    ///     hold axis arrived: 354 banked steps inside 1,087 judged notes is 0.674 against a
    ///     folder p90 of 0.610, and a new true measurement is not those bugs returning. The
    ///     jacks stay exactly as refused as they were.
    /// </summary>
    [Fact]
    public void ThatKittyClaimsHoldHeavyOnceTheNoteCountArrives()
    {
        var folder = new Folder();
        for (var i = 0; i < 20; i++)
        {
            var geometry = Geometry(mid6: 0.87m, sideOn: 0.28m + i * 0.003m)
                .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);
            geometry[PiuCenterMetrics.HoldShare] = 0.35m + i * 0.013m;
            folder.AddCharts(1, geometry, ("mid6_doubles", 0.5m), ("jack", 0.45m));
        }

        var kittyGeometry = Geometry(mid6: 0.958m, sideOn: 0.286m)
            .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);
        kittyGeometry[PiuCenterMetrics.TapRows] = 354m;
        kittyGeometry[PiuCenterMetrics.HoldTicks] = 740m;
        var thatKitty = Folder.Profile(new[] { ("jack", 0.4286m) },
                peakiness: 0.17m, geometry: kittyGeometry)
            .WithNoteCount(1087);

        var chips = folder.ChipsFor(thatKitty);

        Assert.Equal(new[] { IdentityClaimKeys.HoldHeavy }, IdentityBadges(chips).ToArray());
    }

    /// <summary>
    ///     §1, and the owner's own words: "if a chart is like… just a chart, it's fine for it to
    ///     be nothing." A build that invents a claim for That Kitty is wrong. (Since 2026-08-29
    ///     the real D22 earns Hold-heavy from its note count — the fact above — so this chart
    ///     models it as it stood WITHOUT hold data: every other axis still has to stay silent.)
    /// </summary>
    [Fact]
    public void AChartThatEarnsNothingClaimsNothing()
    {
        // The real D22 numbers: its 28.6% side-on sits under the folder's p90 of 32.3%, and its
        // 95.8% middle-six is neither confined nor wide. Ordinary in every direction.
        var folder = new Folder().AddCharts(20, Geometry(mid6: 0.87m, sideOn: 0.28m),
            ("mid6_doubles", 0.5m), ("jack", 0.45m));
        // No picks either: with the union a pick IS an identity claim, so "nothing" now means
        // neither we nor piucenter found anything to say about the chart.
        var thatKitty = Folder.Profile(new[] { ("jack", 0.4286m) },
            peakiness: 0.17m, geometry: Geometry(mid6: 0.958m, sideOn: 0.286m));

        var chips = folder.ChipsFor(thatKitty);

        // Nothing at all, and that is the answer: with features off the card and no pick to
        // carry, a chart that earns no claim says nothing rather than filling the space.
        Assert.Empty(chips.Where(c => c.Tier == IdentityTier.Identity));
    }

    /// <summary>
    ///     §3.2. Claims stack rather than laddering — DUEL SC D23 is a half-double AND
    ///     twist-heavy, and naming only the first describes a different chart.
    /// </summary>
    [Fact]
    public void GeometryClaimsStackWhenAChartEarnsBoth()
    {
        var folder = new Folder().AddCharts(20, Geometry(sideOn: 0.20m), ("run", 0.5m));
        var duel = Folder.Profile(Array.Empty<(string, decimal)>(),
            geometry: Geometry(mid6: 1.0m, sideOn: 0.539m, crossed: 0.305m));

        var badges = folder.ChipsFor(duel).Select(c => c.Badge).ToArray();

        Assert.Contains(IdentityClaimKeys.HalfDouble, badges);
        Assert.Contains(IdentityClaimKeys.TwistHeavy, badges);
    }

    /// <summary>
    ///     §8, Uranium D24 and Cygnus D23: thin coverage everywhere. We claim nothing of our own,
    ///     so what the chart says is exactly what piucenter said — carried as identity now rather
    ///     than as a muted fallback, because a second opinion is still an opinion.
    /// </summary>
    [Fact]
    public void AChartThatStandsOutNowhereSaysWhatPiucenterSaid()
    {
        var folder = new Folder().AddCharts(20, Geometry(), ("mid6_doubles", 0.6m), ("run", 0.5m));
        var uranium = Folder.Profile(new[] { ("mid6_doubles", 0.1m) },
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                { ["jack"] = 1, ["twist_far"] = 2, ["10-stair"] = 3 },
            geometry: Geometry());

        var chips = folder.ChipsFor(uranium);

        // Their three, in their order. The geometry claims are a separate axis and may also
        // fire — this asserts what the BADGES say, which is the part piucenter has an opinion on.
        Assert.Equal(new[] { "jack", "twist_far", "10-stair" },
            IdentityBadges(chips).Where(b => !IdentityClaimKeys.IsGeometryClaim(b)));
    }

    /// <summary>
    ///     The ✦ rule at the bottom of the ladder: at low levels a bracket existing at all is
    ///     the chart's whole identity, and the rule fires on its own the first folder one shows
    ///     up in — no per-level table anywhere.
    /// </summary>
    [Fact]
    public void TheFirstChartInAFolderToCarryABadgeAtAllIsMarkedUnique()
    {
        var folder = new Folder().AddCharts(30, Geometry(), ("jack", 0.5m), ("doublestep", 0.4m));
        var oddOneOut = Folder.Profile(new[] { ("bracket", 0.4m), ("jack", 0.5m) }, geometry: Geometry());

        var chips = folder.ChipsFor(oddOneOut);

        Assert.Equal(new[] { "bracket" }, Badges(chips, IdentityChipKind.Unique));
        Assert.DoesNotContain("jack", Badges(chips, IdentityChipKind.Unique));
    }

    /// <summary>
    ///     Whole-chart qualities carry no coverage, so they never claim one: the chip shows the
    ///     quality and nothing else, where a measured badge carries its number.
    /// </summary>
    [Fact]
    public void WholeChartQualitiesRenderWithoutACoverageNumber()
    {
        var folder = new Folder().AddCharts(20, Geometry(tension: 20m), ("run", 0.5m));
        var sustained = Folder.Profile(new[] { ("run", 0.8m) },
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["sustained"] = 1 },
            geometry: Geometry(tension: 141m));

        var chips = folder.ChipsFor(sustained);

        var chip = Assert.Single(chips.Where(c => c.Badge == "sustained"));
        Assert.Null(chip.Detail);
        Assert.NotNull(chips.Single(c => c.Badge == "run").Detail);
    }
}
