using System;
using System.Collections.Generic;
using System.Linq;
using ScoreTracker.Domain.Records;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Components;
using ScoreTracker.Web.Services;
using ScoreTracker.Web.Services.Theming;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The composer is where the download settings become a tile: the boundary ladder, the
///     broken-score gating, the PUMBILITY chip's two meanings, and the legend. Both Download
///     buttons ride it, which is why it is pinned here at the lowest rung.
/// </summary>
public sealed class ShareCardComposerTests
{
    private static readonly MixEnum Mix = MixEnum.Phoenix;
    private static readonly MixPalette Palette = MixThemes.PaletteFor(Mix);
    private static readonly string Gold = MixThemes.RarityHex(Mix, RarityBand.Gold);

    private static readonly ShareCardOptions AllBoundaries = ShareCardOptions.Default with
    {
        BoundaryOtherMixes = true, BoundaryTop50 = true
    };

    private static Chart BuildChart()
    {
        var song = new Song("District 1", SongType.Arcade, new Uri("https://example.invalid/d1.png"),
            TimeSpan.FromMinutes(2), "Doin", null);
        return new Chart(Guid.NewGuid(), Mix, song, ChartType.Single, DifficultyLevel.From(20), Mix, null, null);
    }

    private static ShareCardComposer.TileFacts Facts(
        PhoenixScore? score = null, PhoenixPlate? plate = null, bool passed = false, bool broken = false,
        bool todo = false, bool other = false, bool top50Type = false, bool top50Combined = false,
        double? gain = null, PhoenixScore? expected = null, double? current = null,
        IReadOnlyList<TierListChartCard.CardSkillChip>? skills = null)
    {
        return new ShareCardComposer.TileFacts(BuildChart(), score, plate, passed, broken, todo, other,
            top50Type, top50Combined, gain, expected, current, skills, null);
    }

    private static TierListShareCard.Tile Compose(ShareCardComposer.TileFacts facts, ShareCardOptions options)
    {
        return ShareCardComposer.Compose(facts, options, Mix, Palette);
    }

    // --- The boundary ladder -----------------------------------------------------------

    [Fact]
    public void ToDoOverridesEveryOtherBoundary()
    {
        var facts = Facts(score: PhoenixScore.From(957_320), plate: PhoenixPlate.SuperbGame,
            passed: true, todo: true, other: true, top50Type: true, top50Combined: true);

        var tile = Compose(facts, AllBoundaries);

        Assert.Equal(MixPalette.Info, tile.BadgeHex);
        Assert.Equal(TileOutline.Dashed, tile.Outline);
    }

    [Fact]
    public void Top50CombinedOutranksTop50TypeOutranksPass()
    {
        var both = Compose(Facts(passed: true, top50Type: true, top50Combined: true), AllBoundaries);
        Assert.Equal(Gold, both.BadgeHex);
        Assert.Equal(TileOutline.Solid, both.Outline);

        var typeOnly = Compose(Facts(passed: true, top50Type: true), AllBoundaries);
        Assert.Equal(Gold, typeOnly.BadgeHex);
        Assert.Equal(TileOutline.Dotted, typeOnly.Outline);

        var passOnly = Compose(Facts(passed: true), AllBoundaries);
        Assert.Equal(MixPalette.Success, passOnly.BadgeHex);
        Assert.Equal(TileOutline.Solid, passOnly.Outline);
    }

    [Fact]
    public void PassOutranksOtherMixOutranksBroken()
    {
        var otherMix = Compose(Facts(other: true, broken: true), AllBoundaries);
        Assert.Equal(MixPalette.Success, otherMix.BadgeHex);
        Assert.Equal(TileOutline.Dashed, otherMix.Outline);

        var brokenOnly = Compose(Facts(broken: true), AllBoundaries);
        Assert.Equal(Palette.InkMuted, brokenOnly.BadgeHex);
        Assert.Equal(TileOutline.Dotted, brokenOnly.Outline);
    }

    [Fact]
    public void ALegacyPassWithNoPhoenixNumberStillWearsThePassBorder()
    {
        var tile = Compose(Facts(passed: true), ShareCardOptions.Default);

        Assert.Equal(MixPalette.Success, tile.BadgeHex);
        Assert.Equal(TileOutline.Solid, tile.Outline);
        Assert.Null(tile.GradeUrl);
    }

    [Fact]
    public void DisabledBoundariesLeaveTheTileBare()
    {
        var options = ShareCardOptions.Default with
        {
            BoundaryTodo = false, BoundaryPass = false, BoundaryBroken = false
        };

        var tile = Compose(Facts(passed: true, todo: true, broken: true), options);

        Assert.Null(tile.BadgeHex);
        Assert.Equal(TileOutline.Dot, tile.Outline);
    }

    // --- The color modes ----------------------------------------------------------------

    [Fact]
    public void ColorByLetterGradeTakesTheGradesOwnHexAndOnlyAPerfectGameGlows()
    {
        var options = ShareCardOptions.Default with { ColorByLetterGrade = true };
        var score = PhoenixScore.From(981_000);

        var plain = Compose(Facts(score: score, plate: PhoenixPlate.SuperbGame, passed: true), options);
        Assert.Equal(MixThemes.GradeHex(score.LetterGradeFor(Mix).GetName()), plain.BadgeHex);
        Assert.Equal(TileOutline.Solid, plain.Outline);
        Assert.False(plain.Glow);

        var pg = Compose(Facts(score: PhoenixScore.From(1_000_000), plate: PhoenixPlate.PerfectGame, passed: true),
            options);
        Assert.True(pg.Glow);
    }

    [Fact]
    public void ColorByPlateTakesThePlatesOwnHex()
    {
        var options = ShareCardOptions.Default with { ColorByPlate = true };

        var tile = Compose(Facts(score: PhoenixScore.From(957_320), plate: PhoenixPlate.TalentedGame, passed: true),
            options);

        Assert.Equal(MixThemes.PlateHex(PhoenixPlate.TalentedGame.GetShorthand()), tile.BadgeHex);
        Assert.False(tile.Glow);
    }

    [Fact]
    public void APlainPassNeverGlowsEvenOnAPerfectGame()
    {
        var tile = Compose(Facts(score: PhoenixScore.From(1_000_000), plate: PhoenixPlate.PerfectGame, passed: true),
            ShareCardOptions.Default);

        Assert.False(tile.Glow);
        Assert.Equal(MixPalette.Success, tile.BadgeHex);
    }

    // --- Scores -------------------------------------------------------------------------

    [Fact]
    public void TheScorePrintsPlainAndBrokenPrintsMuted()
    {
        var options = ShareCardOptions.Default with { Scores = true };

        var passTile = Compose(Facts(score: PhoenixScore.From(957_320), passed: true), options);
        Assert.Equal("957,320", passTile.ScoreLabel);
        Assert.False(passTile.ScoreMuted);

        var brokenTile = Compose(Facts(score: PhoenixScore.From(921_540), broken: true), options);
        Assert.Equal("921,540", brokenTile.ScoreLabel);
        Assert.True(brokenTile.ScoreMuted);
    }

    [Fact]
    public void ExcludingBrokenRunsDropsOnlyTheBrokenScore()
    {
        var options = ShareCardOptions.Default with { Scores = true, IncludeBrokenScores = false };

        var brokenTile = Compose(Facts(score: PhoenixScore.From(921_540), broken: true), options);
        Assert.Null(brokenTile.ScoreLabel);

        var passTile = Compose(Facts(score: PhoenixScore.From(957_320), passed: true), options);
        Assert.Equal("957,320", passTile.ScoreLabel);
    }

    [Fact]
    public void ScoresOffPrintsNothingEvenWithAScore()
    {
        var tile = Compose(Facts(score: PhoenixScore.From(957_320), passed: true), ShareCardOptions.Default);

        Assert.Null(tile.ScoreLabel);
    }

    // --- The PUMBILITY corner -----------------------------------------------------------

    [Fact]
    public void PumbilityAlonePrintsTheCurrentValueAtPoolPrecision()
    {
        var options = ShareCardOptions.Default with { Pumbility = true };

        var tile = Compose(Facts(score: PhoenixScore.From(957_320), passed: true, current: 138.246), options);

        Assert.Equal(138.246.ToString("N2"), tile.CornerLabel);
        Assert.Equal(Palette.Primary, tile.CornerHex);
        Assert.Null(tile.ExpectedGradeUrl);
    }

    [Fact]
    public void AZeroValueChartSaysNothing()
    {
        var options = ShareCardOptions.Default with { Pumbility = true };

        var tile = Compose(Facts(score: PhoenixScore.From(921_540), broken: true, current: 0), options);

        Assert.Null(tile.CornerLabel);
    }

    [Fact]
    public void ExpectedGainsSwitchesTheChipToTheGainAndItsGrade()
    {
        var options = ShareCardOptions.Default with { Pumbility = true, ExpectedGains = true };
        var expected = PhoenixScore.From(964_000);

        var tile = Compose(Facts(gain: 12.46, expected: expected, current: 138.2), options);

        Assert.Equal($"+{PumbilityFormat.Gain(12.46)}", tile.CornerLabel);
        Assert.Equal(Gold, tile.CornerHex);
        Assert.Equal(ShareCardImages.LetterGrade(expected.LetterGradeFor(Mix), false), tile.ExpectedGradeUrl);
    }

    [Fact]
    public void ExpectedGainsSaysNothingWhereThereIsNoGain()
    {
        var options = ShareCardOptions.Default with { Pumbility = true, ExpectedGains = true };

        var noGain = Compose(Facts(score: PhoenixScore.From(1_000_000), passed: true, current: 158.0), options);

        Assert.Null(noGain.CornerLabel);
        Assert.Null(noGain.ExpectedGradeUrl);
    }

    // --- Names, marks and skills --------------------------------------------------------

    [Fact]
    public void EveryComposedTileUsesTheCompactMarksLayout()
    {
        Assert.True(Compose(Facts(), ShareCardOptions.Default).CompactMarks);
    }

    [Fact]
    public void SongNamesRideTheCaptionAndGradeArtFollowsTheToggles()
    {
        var options = ShareCardOptions.Default with { SongNames = true };
        var score = PhoenixScore.From(957_320);

        var tile = Compose(Facts(score: score, plate: PhoenixPlate.SuperbGame, passed: true), options);

        Assert.Equal("District 1", tile.Caption);
        Assert.Equal(ShareCardImages.LetterGrade(score.LetterGradeFor(Mix), false), tile.GradeUrl);
        Assert.Equal(ShareCardImages.Plate(PhoenixPlate.SuperbGame), tile.PlateUrl);

        var bare = Compose(Facts(score: score, plate: PhoenixPlate.SuperbGame, passed: true),
            options with { LetterGrades = false, Plates = false });
        Assert.Null(bare.GradeUrl);
        Assert.Null(bare.PlateUrl);
    }

    [Fact]
    public void SkillChipsKeepIdentityClaimsOnlyAndResolveTheirFamilyHex()
    {
        var options = ShareCardOptions.Default with { Skills = true };
        var skills = new[]
        {
            new TierListChartCard.CardSkillChip("Twists", "badgecat-twists", null, IsIdentity: true),
            new TierListChartCard.CardSkillChip("Runs", "badgecat-staminaandruns", "8.2", IsIdentity: true),
            new TierListChartCard.CardSkillChip("Also has", "chip-fallback", null)
        };

        var tile = Compose(Facts(skills: skills), options);

        Assert.NotNull(tile.SkillChips);
        Assert.Equal(2, tile.SkillChips!.Count);
        Assert.Equal("Twists", tile.SkillChips[0].Label);
        Assert.Equal(MixThemes.SkillClassHex("badgecat-twists"), tile.SkillChips[0].Hex);
        Assert.Equal("Runs 8.2", tile.SkillChips[1].Label);

        var neutral = Compose(Facts(skills: new[]
        {
            new TierListChartCard.CardSkillChip("Hardest 20s:", "chip-section", null, IsIdentity: true)
        }), options);
        Assert.Equal(Palette.InkMuted, neutral.SkillChips!.Single().Hex);
    }

    // --- The legend ---------------------------------------------------------------------

    [Fact]
    public void TheLegendPrintsExactlyTheEnabledBoundaries()
    {
        var legend = ShareCardComposer.Legend(AllBoundaries, Mix, Palette, s => s)!;

        Assert.Equal(new[]
        {
            "To Do", "Pass", "Passed in other mixes", "Broken with score",
            "Top 50 — this chart's type", "Top 50 — combined"
        }, legend.Select(e => e.Label).ToArray());
        Assert.Equal(TileOutline.Dotted, legend.Single(e => e.Label == "Top 50 — this chart's type").Outline);
        Assert.Equal(TileOutline.Solid, legend.Single(e => e.Label == "Top 50 — combined").Outline);
    }

    [Fact]
    public void TheColorModesCollapseToOneLegendEntry()
    {
        var byGrade = ShareCardComposer.Legend(
            ShareCardOptions.Default with { ColorByLetterGrade = true }, Mix, Palette, s => s)!;

        Assert.Contains("Pass — colored by letter grade · PG glows", byGrade.Select(e => e.Label));
        Assert.DoesNotContain("Pass", byGrade.Select(e => e.Label));
    }

    [Fact]
    public void NoBoundariesMeansNoLegend()
    {
        var options = ShareCardOptions.Default with
        {
            BoundaryTodo = false, BoundaryPass = false, BoundaryBroken = false
        };

        Assert.Null(ShareCardComposer.Legend(options, Mix, Palette, s => s));
    }

    // --- The current-value math ---------------------------------------------------------

    [Fact]
    public void CurrentPumbilityUsesThePoolPagesOwnArithmetic()
    {
        var chart = BuildChart();
        var value = ShareCardComposer.CurrentPumbility(chart, PhoenixScore.From(957_320),
            PhoenixPlate.SuperbGame, false, Mix);

        Assert.NotNull(value);
        Assert.True(value > 0);
        // A broken run prices at zero — the pool's own StageBreakModifier — and the chip
        // then stays silent rather than printing 0.00.
        Assert.Equal(0, ShareCardComposer.CurrentPumbility(chart, PhoenixScore.From(921_540),
            PhoenixPlate.RoughGame, true, Mix));
    }

    [Fact]
    public void CurrentPumbilityHasNoAnswerForLegacyMixesOrMissingScores()
    {
        var chart = BuildChart();

        Assert.Null(ShareCardComposer.CurrentPumbility(chart, PhoenixScore.From(950_000), null, false, MixEnum.XX));
        Assert.Null(ShareCardComposer.CurrentPumbility(chart, null, null, false, Mix));
    }
}
