using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using ScoreTracker.Catalog.Contracts;
using ScoreTracker.Web;
using ScoreTracker.Web.Services;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     How a chart's identity reaches a card (docs/design/chart-identity.md §3). The kinds have
///     to stay visually distinct: a chip claiming "almost nothing else here has this" and one
///     claiming "this is what the chart is made of" are different statements, and a reader who
///     cannot tell them apart is reading neither.
/// </summary>
public sealed class IdentityChipsTests : ComponentTestBase
{
    private IStringLocalizer Localizer => Services.GetRequiredService<IStringLocalizer<App>>();

    private static ChartIdentityRecord Identity(params IdentityChipRecord[] chips)
    {
        return new ChartIdentityRecord(Guid.NewGuid(), chips);
    }

    private static IdentityChipRecord Chip(IdentityChipKind kind, string badge, string display,
        BadgeCategory? family = BadgeCategory.Brackets, decimal? detail = null)
    {
        return new IdentityChipRecord(kind, badge, display, family, detail);
    }

    [Fact]
    public void EachKindCarriesItsOwnClassSoTheClaimsReadApart()
    {
        var chips = IdentityChips.ToCardChips(Identity(
            Chip(IdentityChipKind.Unique, "split", "Splits", BadgeCategory.DoublesTech, 0.4m),
            Chip(IdentityChipKind.Core, "bracket", "Brackets", BadgeCategory.Brackets, 0.5m),
            Chip(IdentityChipKind.Spike, string.Empty, string.Empty, null, 1.3m),
            Chip(IdentityChipKind.Crux, "twist_90", "Twist 90", BadgeCategory.Twists)), false, Localizer);

        Assert.Contains("chip-unique", chips[0].CategoryClass);
        Assert.Contains("badgecat-doublestech", chips[0].CategoryClass);
        // Core is the plain chip: family tint only, no kind marker.
        Assert.Equal("badgecat-brackets", chips[1].CategoryClass);
        Assert.Contains("chip-spike", chips[2].CategoryClass);
        Assert.Contains("chip-crux", chips[3].CategoryClass);
    }

    /// <summary>
    ///     The spike is a shape, not a skill. Borrowing a family's colour would file it as one,
    ///     and the number IS the chip rather than an annotation on a badge.
    /// </summary>
    [Fact]
    public void TheSpikeChipCarriesNoFamilyTintAndPrintsItsSignedOffset()
    {
        var chips = IdentityChips.ToCardChips(
            Identity(Chip(IdentityChipKind.Spike, string.Empty, string.Empty, null, 1.3m)), false, Localizer);

        var spike = Assert.Single(chips);
        Assert.DoesNotContain("badgecat-", spike.CategoryClass);
        Assert.Equal("+1.3", spike.Metric);
    }

    /// <summary>
    ///     Coverage is meaningless without the folder around it, so it stays behind the
    ///     existing Show Skill Metric preference rather than shouting a bare percentage.
    /// </summary>
    [Fact]
    public void CoverageOnlyPrintsWhenTheReaderAskedForIt()
    {
        var identity = Identity(Chip(IdentityChipKind.Core, "bracket", "Brackets", BadgeCategory.Brackets, 0.42m));

        Assert.Null(IdentityChips.ToCardChips(identity, false, Localizer).Single().Metric);
        Assert.Equal("42%", IdentityChips.ToCardChips(identity, true, Localizer).Single().Metric);
    }

    [Fact]
    public void AWholeChartQualityShowsNoCoverageEvenWhenNumbersAreOn()
    {
        var chips = IdentityChips.ToCardChips(
            Identity(Chip(IdentityChipKind.Core, "sustained", "Sustained", BadgeCategory.StaminaAndRuns)),
            true, Localizer);

        Assert.Null(Assert.Single(chips).Metric);
    }

    /// <summary>
    ///     The Chabala lens's tags map to nothing and must wear nothing: tinting them would
    ///     file the retired vocabulary under the badge families, which is the association the
    ///     rollup's removal exists to end.
    /// </summary>
    [Fact]
    public void ArchivedHandTagsRenderNeutralWithNoFamilyClass()
    {
        var chips = IdentityChips.ToArchivedChips(new[] { "Brackets & Runs", "Half-Double" });

        Assert.Equal(2, chips.Count);
        Assert.All(chips, c => Assert.Equal(string.Empty, c.CategoryClass));
        Assert.All(chips, c => Assert.Null(c.Metric));
        Assert.Equal("Brackets & Runs", chips[0].Label);
    }

    [Fact]
    public void AChartWithNoBankedAnalysisShowsNothingRatherThanAnEmptyRow()
    {
        Assert.Empty(IdentityChips.ToCardChips(null, true, Localizer));
        Assert.Empty(IdentityChips.ToArchivedChips(null));
    }
}
