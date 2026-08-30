using ScoreTracker.Domain.Records;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Components;
using ScoreTracker.Web.Services.Theming;

namespace ScoreTracker.Web.Services;

/// <summary>
///     Applies <see cref="ShareCardOptions" /> to one chart's facts and hands back the tile the
///     renderer draws (docs/design/share-card-download-settings.md). Both Download buttons feed
///     this — the boundary ladder, the score gating and the chip switching live HERE and nowhere
///     else, so the two pictures cannot disagree about what a border means.
/// </summary>
public static class ShareCardComposer
{
    /// <summary>
    ///     Everything the composer may print about one chart, already resolved by the page.
    ///     <paramref name="Passed" /> and <paramref name="Broken" /> arrive as facts rather than
    ///     being derived from <paramref name="Score" /> — a legacy best carries no Phoenix number
    ///     yet still passed, and the boundary must say so.
    /// </summary>
    public sealed record TileFacts(
        Chart Chart,
        PhoenixScore? Score,
        PhoenixPlate? Plate,
        bool Passed,
        bool Broken,
        bool IsToDo,
        bool PassedInOtherMix,
        bool InTop50Type,
        bool InTop50Combined,
        double? Gain,
        PhoenixScore? ExpectedScore,
        double? CurrentPumbility,
        IReadOnlyList<TierListChartCard.CardSkillChip>? Skills,
        string? BubbleUrl);

    public static TierListShareCard.Tile Compose(TileFacts f, ShareCardOptions o, MixEnum mix, MixPalette palette)
    {
        var (badgeHex, outline, glow) = Boundary(f, o, mix, palette);

        string? scoreLabel = null;
        var scoreMuted = false;
        if (o.Scores && f.Score is { } score && (!f.Broken || o.IncludeBrokenScores))
        {
            scoreLabel = ((int)score).ToString("N0");
            scoreMuted = f.Broken;
        }

        // The PUMBILITY corner (design doc §2): with Expected gains, the gain chip and the
        // expected-grade art — only where there IS a gain; without it, the chart's current
        // value in the pool corner's own N2. Zero-value charts say nothing.
        string? corner = null;
        string? cornerHex = null;
        string? expectedUrl = null;
        if (o.Pumbility)
        {
            if (o.ExpectedGains)
            {
                if (f.Gain is > 0)
                {
                    corner = $"+{PumbilityFormat.Gain(f.Gain.Value)}";
                    cornerHex = MixThemes.RarityHex(mix, RarityBand.Gold);
                    if (f.ExpectedScore is { } expected)
                        expectedUrl = ShareCardImages.LetterGrade(expected.LetterGradeFor(mix), false);
                }
            }
            else if (f.CurrentPumbility is > 0)
            {
                corner = f.CurrentPumbility.Value.ToString("N2");
                cornerHex = palette.Primary;
            }
        }

        var gradeUrl = o.LetterGrades && f.Score is { } graded
            ? ShareCardImages.LetterGrade(graded.LetterGradeFor(mix), f.Broken)
            : null;
        var plateUrl = o.Plates && f.Plate is { } plate ? ShareCardImages.Plate(plate) : null;

        IReadOnlyList<TierListShareCard.SkillChip>? chips = null;
        if (o.Skills && f.Skills is { Count: > 0 })
        {
            var identity = f.Skills.Where(s => s.IsIdentity)
                .Select(s => new TierListShareCard.SkillChip(ChipLabel(s),
                    MixThemes.SkillClassHex(s.CategoryClass) ?? palette.InkMuted))
                .ToArray();
            chips = identity.Length > 0 ? identity : null;
        }

        return new TierListShareCard.Tile(
            f.Chart.Song.ImagePath.ToString(), gradeUrl, plateUrl, badgeHex,
            corner, cornerHex, outline, f.BubbleUrl,
            o.SongNames ? f.Chart.Song.Name.ToString() : null,
            scoreLabel, scoreMuted, expectedUrl, glow, chips, CompactMarks: true);
    }

    /// <summary>
    ///     One border per tile, most specific enabled claim first (design doc §3): To Do beats
    ///     everything, then Top 50 combined, Top 50 type, Pass in its three forms, the other-mix
    ///     pass, and broken-with-score last — the page's own tail order.
    /// </summary>
    private static (string? Hex, TileOutline Outline, bool Glow) Boundary(TileFacts f, ShareCardOptions o,
        MixEnum mix, MixPalette palette)
    {
        if (o.BoundaryTodo && f.IsToDo) return (MixPalette.Info, TileOutline.Dashed, false);
        if (o.BoundaryTop50 && f.InTop50Combined)
            return (MixThemes.RarityHex(mix, RarityBand.Gold), TileOutline.Solid, false);
        if (o.BoundaryTop50 && f.InTop50Type)
            return (MixThemes.RarityHex(mix, RarityBand.Gold), TileOutline.Dotted, false);
        if (o.BoundaryPass && f.Passed)
        {
            // The Perfect Game's halo belongs to the color modes alone — a plain green pass
            // never glows, so PG stays a color-mode reward rather than sitewide noise.
            var pg = f.Plate == PhoenixPlate.PerfectGame;
            if (o.ColorByLetterGrade && f.Score is { } score)
                return (MixThemes.GradeHex(score.LetterGradeFor(mix).GetName()), TileOutline.Solid, pg);
            if (o.ColorByPlate && f.Plate is { } plate)
                return (MixThemes.PlateHex(plate.GetShorthand()), TileOutline.Solid, pg);
            return (MixPalette.Success, TileOutline.Solid, false);
        }

        if (o.BoundaryOtherMixes && f.PassedInOtherMix) return (MixPalette.Success, TileOutline.Dashed, false);
        if (o.BoundaryBroken && f.Broken) return (palette.InkMuted, TileOutline.Dotted, false);
        return (null, TileOutline.Dot, false);
    }

    /// <summary>
    ///     The legend the card's footer prints: exactly the boundaries that are on, with the two
    ///     color modes collapsed to a single "colored by" entry (design doc §4). Labels arrive
    ///     through <paramref name="localize" /> so the composer stays testable with identity.
    /// </summary>
    public static IReadOnlyList<TierListShareCard.LegendEntry>? Legend(ShareCardOptions o, MixEnum mix,
        MixPalette palette, Func<string, string> localize)
    {
        var entries = new List<TierListShareCard.LegendEntry>();
        if (o.BoundaryTodo)
            entries.Add(new TierListShareCard.LegendEntry(localize("To Do"), MixPalette.Info, TileOutline.Dashed));
        if (o.BoundaryPass)
        {
            if (o.ColorByLetterGrade)
                entries.Add(new TierListShareCard.LegendEntry(localize("Pass — colored by letter grade · PG glows"),
                    MixThemes.GradeHex(PhoenixLetterGrade.SS.GetName()), TileOutline.Solid));
            else if (o.ColorByPlate)
                entries.Add(new TierListShareCard.LegendEntry(localize("Pass — colored by plate · PG glows"),
                    MixThemes.PlateHex(PhoenixPlate.ExtremeGame.GetShorthand()), TileOutline.Solid));
            else
                entries.Add(new TierListShareCard.LegendEntry(localize("Pass"), MixPalette.Success,
                    TileOutline.Solid));
        }

        if (o.BoundaryOtherMixes)
            entries.Add(new TierListShareCard.LegendEntry(localize("Passed in other mixes"), MixPalette.Success,
                TileOutline.Dashed));
        if (o.BoundaryBroken)
            entries.Add(new TierListShareCard.LegendEntry(localize("Broken with score"), palette.InkMuted,
                TileOutline.Dotted));
        if (o.BoundaryTop50)
        {
            var gold = MixThemes.RarityHex(mix, RarityBand.Gold);
            entries.Add(new TierListShareCard.LegendEntry(localize("Top 50 — this chart's type"), gold,
                TileOutline.Dotted));
            entries.Add(new TierListShareCard.LegendEntry(localize("Top 50 — combined"), gold, TileOutline.Solid));
        }

        return entries.Count > 0 ? entries : null;
    }

    /// <summary>
    ///     The chart's current worth under the mix's PUMBILITY formula — the pool page's own
    ///     arithmetic (<see cref="ScoringConfiguration.PumbilityScoring" /> with the pool's own
    ///     RoughGame fallback), so the chip and /Pumbility can never disagree about one chart.
    ///     Null where the mix has no PUMBILITY formula at all.
    /// </summary>
    public static double? CurrentPumbility(Chart chart, PhoenixScore? score, PhoenixPlate? plate, bool isBroken,
        MixEnum mix)
    {
        if (score == null || mix is not (MixEnum.Phoenix or MixEnum.Phoenix2)) return null;
        return ScoringConfiguration.PumbilityScoring(mix, false)
            .GetScore(chart, score.Value, plate ?? PhoenixPlate.RoughGame, isBroken);
    }

    private static string ChipLabel(TierListChartCard.CardSkillChip chip)
    {
        var label = chip.Metric == null ? chip.Label : $"{chip.Label} {chip.Metric}";
        return chip.Parts is { Count: > 0 }
            ? $"{label} {string.Join(", ", chip.Parts.Select(p => p.Label))}"
            : label;
    }
}
