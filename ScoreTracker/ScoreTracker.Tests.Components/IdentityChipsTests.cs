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
        BadgeCategory? family = BadgeCategory.Brackets, decimal? detail = null,
        IdentityTier tier = IdentityTier.Identity, IReadOnlyList<IdentityChipBadge>? badges = null)
    {
        return new IdentityChipRecord(kind, tier, badge, display, family, detail, badges);
    }

    [Fact]
    public void EachKindCarriesItsOwnClassSoTheClaimsReadApart()
    {
        var chips = IdentityChips.ToCardChips(Identity(
            Chip(IdentityChipKind.Unique, "split", "Splits", BadgeCategory.DoublesTech, 0.4m),
            Chip(IdentityChipKind.Core, "bracket", "Brackets", BadgeCategory.Brackets, 0.5m),
            Chip(IdentityChipKind.Spike, string.Empty, string.Empty, null, 1.3m),
            Chip(IdentityChipKind.Width, "Half-Double", "Half-Double", null)), false, Localizer);

        Assert.Contains("chip-unique", chips[0].CategoryClass);
        Assert.Contains("badgecat-doublestech", chips[0].CategoryClass);
        // Core is the plain chip: family tint only, no kind marker.
        Assert.Equal("badgecat-brackets", chips[1].CategoryClass);
        Assert.Contains("chip-spike", chips[2].CategoryClass);
        // Shape claims sit outside the five families, like the spike.
        Assert.Contains("chip-geometry", chips[3].CategoryClass);
        Assert.DoesNotContain("badgecat-", chips[3].CategoryClass);
    }

    /// <summary>
    ///     One window, so one chip: the owner's own line for BSPower was "Hardest 10s: Drills
    ///     90 degree twists". Each badge keeps its family inside the shared chip, so merging the
    ///     pair costs nothing a reader could have used.
    /// </summary>
    [Fact]
    public void TheHardSectionChipCarriesItsLengthAndBothBadgesFamilies()
    {
        var chips = IdentityChips.ToCardChips(Identity(
            Chip(IdentityChipKind.HardSection, string.Empty, string.Empty, null, 9.75m,
                badges: new[]
                {
                    new IdentityChipBadge("drill", "Drills", BadgeCategory.StaminaAndRuns),
                    new IdentityChipBadge("twist_90", "90° Twists", BadgeCategory.Twists)
                })), false, Localizer);

        var section = Assert.Single(chips);
        Assert.Contains("10", section.Label);
        Assert.Contains("chip-section", section.CategoryClass);
        Assert.Equal(new[] { "Drills", "90° Twists" }, section.Parts!.Select(p => p.Label));
        Assert.Contains("badgecat-staminaandruns", section.Parts![0].CategoryClass);
        Assert.Contains("badgecat-twists", section.Parts![1].CategoryClass);
    }

    /// <summary>
    ///     The tier reaches the card as a flag, because the card draws the two groups itself —
    ///     one row labelled Identity, one labelled Features.
    /// </summary>
    [Fact]
    public void TheTierTravelsToTheCardSoTheGroupsCanBeNamed()
    {
        var chips = IdentityChips.ToCardChips(Identity(
            Chip(IdentityChipKind.Core, "bracket", "Brackets", BadgeCategory.Brackets, 0.5m),
            Chip(IdentityChipKind.Core, "drill", "Drills", BadgeCategory.StaminaAndRuns, 0.4m,
                IdentityTier.Feature)), false, Localizer);

        Assert.True(chips[0].IsIdentity);
        Assert.False(chips[1].IsIdentity);
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
        // Named in words: an arrow and a number said nothing to anyone not already told what
        // it meant (owner, 2026-08-26).
        Assert.Equal("Difficulty Spike", spike.Label);
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
