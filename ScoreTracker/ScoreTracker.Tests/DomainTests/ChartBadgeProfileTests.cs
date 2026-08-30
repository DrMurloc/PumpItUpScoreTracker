using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ScoreTracker.Catalog.Domain;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

/// <summary>
///     The seam between the banked metric rows and the chip engine. Every other test in this
///     folder builds a <see cref="ChartBadgeProfile" /> through its constructor, which is exactly
///     how Speed and Longest run could ship green and inert: their metrics were banked, the rules
///     read them, and <see cref="ChartBadgeProfile.From" /> dropped both on the floor in between
///     because its filter set never named them (field test, 2026-08-26).
/// </summary>
public sealed class ChartBadgeProfileTests
{
    /// <summary>
    ///     Scalars the identity engine deliberately does not read, each with the reason. Shrink
    ///     only: adding a metric constant means deciding which side of this line it falls on, and
    ///     the silent failure this test exists to catch is a metric that lands on neither.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> NotReadByTheEngine =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [PiuCenterMetrics.Source] = "the source's name, not a measurement",
            [PiuCenterMetrics.DataVersion] = "provenance — which crawl banked the row",
            [PiuCenterMetrics.HoldRows] = "the score calculator's hold-tick decomposition",
            [PiuCenterMetrics.HoldShare] = "never banked — WithNoteCount derives and writes it",
            [PiuCenterMetrics.DifficultyPrediction] = "their level guess; the tier lists own ours",
            [PiuCenterMetrics.CruxLevel] = "peakiness is this against the printed level",
            [PiuCenterMetrics.CruxPosition] = "where the crux sits, which no chip says",
            [PiuCenterMetrics.CruxEnps] = "the crux's speed, which no chip says",
            [PiuCenterMetrics.LastSegmentIsPeak] = "the recap's ending-strength read",
            [PiuCenterMetrics.PackIsPhoenix] = "which mix's simfile was analyzed — provenance"
        };

    /// <summary>
    ///     Peakiness and duration land on their own properties rather than the geometry bag, so
    ///     they are read out separately below.
    /// </summary>
    private static readonly IReadOnlySet<string> ReadOntoTheirOwnProperty =
        new HashSet<string>(StringComparer.Ordinal)
            { PiuCenterMetrics.CruxPeakiness, PiuCenterMetrics.CruxDuration };

    [Fact]
    public void EveryBankedScalarEitherReachesTheEngineOrSaysWhyItDoesNot()
    {
        var chartId = Guid.NewGuid();
        var scalars = typeof(PiuCenterMetrics)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            // The prefixed families are keyed per badge and have their own dictionaries.
            .Where(name => !name.EndsWith(":", StringComparison.Ordinal))
            .ToArray();

        var profile = ChartBadgeProfile.From(chartId,
            scalars.Select(name => new ChartSkillMetric(chartId, name, 1m, null)));

        var dropped = scalars
            .Where(name => !NotReadByTheEngine.ContainsKey(name))
            .Where(name => !ReadOntoTheirOwnProperty.Contains(name))
            .Where(name => profile.GeometryOf(name) == null)
            .ToArray();

        Assert.True(dropped.Length == 0,
            "These metrics are banked but never reach the chip rules, so a claim reading one " +
            "cannot fire and cannot say why: " + string.Join(", ", dropped));
        Assert.Equal(1m, profile.CruxPeakiness);
        Assert.Equal(1m, profile.CruxDuration);
    }

    /// <summary>
    ///     The two the seam actually swallowed, named outright: a generic scan is only as good as
    ///     its exemption list, and these are the ones a future exemption must not quietly cover.
    /// </summary>
    [Fact]
    public void SpeedAndTheLongestRunReadTheMetricsTheyAreMeasuredFrom()
    {
        var chartId = Guid.NewGuid();
        var profile = ChartBadgeProfile.From(chartId, new[]
        {
            new ChartSkillMetric(chartId, PiuCenterMetrics.Nps, 9.5m, null),
            new ChartSkillMetric(chartId, PiuCenterMetrics.ChartSpan, 120m, null),
            new ChartSkillMetric(chartId, PiuCenterMetrics.SustainTime, 40m, null)
        });

        Assert.Equal(9.5m, profile.GeometryOf(PiuCenterMetrics.Nps));
        Assert.Equal(120m, profile.GeometryOf(PiuCenterMetrics.ChartSpan));
        Assert.Equal(40m, profile.GeometryOf(PiuCenterMetrics.SustainTime));
    }

    /// <summary>
    ///     §3.9. The share is every judgement that is not a banked tap row, over the mix's
    ///     judged total — Iolite Sky D20's 156 steps inside 1,000 notes. The file's own tick
    ///     list (848) comfortably accounts for the 844 inferred, so the reading is believed.
    /// </summary>
    [Fact]
    public void TheNoteCountDerivesTheHoldShareFromTheBankedStepCount()
    {
        var chartId = Guid.NewGuid();
        var profile = ChartBadgeProfile.From(chartId, new[]
        {
            new ChartSkillMetric(chartId, PiuCenterMetrics.TapRows, 156m, null),
            new ChartSkillMetric(chartId, PiuCenterMetrics.HoldTicks, 848m, null)
        }).WithNoteCount(1000);

        Assert.Equal(0.844m, profile.GeometryOf(PiuCenterMetrics.HoldShare));
        Assert.True(profile.HoldsAreCredible);
    }

    /// <summary>
    ///     §3.9. Destination SHORT CUT D20: 278 banked taps against a judged 803 infers 525
    ///     holds, from a file whose own hold list totals 123 ticks. The two numbers cannot both
    ///     be true, so the file is not the shipped chart and the high claim must stay silent —
    ///     a wrong file always errs by INFLATING the holds, because its missing steps read as
    ///     ticks.
    /// </summary>
    [Fact]
    public void AFileThatCannotAccountForTheInferredHoldsIsNotBelieved()
    {
        var chartId = Guid.NewGuid();
        var profile = ChartBadgeProfile.From(chartId, new[]
        {
            new ChartSkillMetric(chartId, PiuCenterMetrics.TapRows, 278m, null),
            new ChartSkillMetric(chartId, PiuCenterMetrics.HoldTicks, 123m, null)
        }).WithNoteCount(803);

        Assert.False(profile.HoldsAreCredible);
        Assert.NotNull(profile.GeometryOf(PiuCenterMetrics.HoldShare));
    }

    /// <summary>
    ///     §3.9. God Mode S4's file carries more taps than the game judges notes — an
    ///     arithmetically impossible file says NOTHING about holds, rather than something
    ///     extreme. And with no note count at all, the profile is untouched.
    /// </summary>
    [Fact]
    public void AnImpossibleOrAbsentNoteCountSaysNothingAboutHolds()
    {
        var chartId = Guid.NewGuid();
        var profile = ChartBadgeProfile.From(chartId, new[]
        {
            new ChartSkillMetric(chartId, PiuCenterMetrics.TapRows, 210m, null),
            new ChartSkillMetric(chartId, PiuCenterMetrics.HoldTicks, 0m, null)
        });

        var impossible = profile.WithNoteCount(208);
        Assert.Null(impossible.GeometryOf(PiuCenterMetrics.HoldShare));
        Assert.True(impossible.HoldsAreCredible);

        Assert.Same(profile, profile.WithNoteCount(null));
    }
}
