using ScoreTracker.Domain.Models.Titles;
using ScoreTracker.Domain.Models.Titles.Phoenix;
using ScoreTracker.Domain.Models.Titles.Phoenix2;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Services.Theming;

namespace ScoreTracker.Web.Services;

/// <summary>Where a rung stands for you. Ordering matters: the filter chips read it.</summary>
public enum RungState
{
    /// <summary>Not started, or started from nothing measurable.</summary>
    Locked,

    /// <summary>Under way — the only state that carries a partial fill.</summary>
    Active,

    Earned
}

/// <summary>
///     The page's grouping of a mix's rails. Section order is the order they render in.
/// </summary>
public enum TitleSection
{
    Progression,
    Skill,
    CoOp,
    Plates,
    BossBreakers,
    StepArtists,
    PlayCount,
    OneOffs
}

/// <param name="Official">
///     True when nothing behind this title is computable here — every title with no
///     requirement (CompletionRequired 0) is awarded by the official import and nothing else.
///     Such a rung is held or not; it never shows partial progress.
/// </param>
/// <param name="Share">Fraction of tracked players holding it, 0 through 1.</param>
public sealed record TitleRung(
    TitleProgress Progress,
    RungState State,
    bool Official,
    double Fraction,
    double Share,
    RarityBand Band)
{
    public Title Title => Progress.Title;
    public int Rung => Progress.Title.Rung;
}

/// <param name="Name">The rail's own name — a folder tier, a skill track, a mix.</param>
public sealed record TitleRailRow(Name Name, IReadOnlyList<TitleRung> Rungs)
{
    public int Earned => Rungs.Count(r => r.State == RungState.Earned);
    public int Total => Rungs.Count;

    /// <summary>The rung a player is working on, if any — the caption speaks for this one.</summary>
    public TitleRung? Active => Rungs.FirstOrDefault(r => r.State == RungState.Active);

    /// <summary>Every rung on this rail comes from the official import, so none of it computes.</summary>
    public bool Official => Rungs.All(r => r.Official);
}

public sealed record TitleSectionRows(
    TitleSection Section,
    IReadOnlyList<TitleRailRow> Rails,
    IReadOnlyList<TitleRung> OneOffs)
{
    public int Total => Rails.Sum(r => r.Total) + OneOffs.Count;
    public int Earned => Rails.Sum(r => r.Earned) + OneOffs.Count(o => o.State == RungState.Earned);
}

/// <summary>
///     Turns a mix's title progress into the rails the page draws. Pure — no I/O, no clock —
///     so the whole layout is testable without rendering anything.
/// </summary>
public static class TitleRails
{
    /// <summary>
    ///     Which section a title's category belongs under. Categories are the mixes' own and
    ///     differ between them: Phoenix files all six skill tracks under "Skill", while Phoenix
    ///     2 gives each track its own category and files both PUMBILITY and its skill ladders
    ///     differently again. Anything unmapped is a one-off, which is the honest default —
    ///     a category we do not recognise has no progression we can claim to draw.
    /// </summary>
    private static TitleSection SectionFor(Title title)
    {
        // SPECIALIST spans every skill track, so Phoenix 2 files it under Misc. rather than
        // under any one of them. It still belongs beside the tracks it is earned from, and
        // its type says so where its category cannot.
        if (title is Phoenix2TitleSetTitle) return TitleSection.Skill;

        var category = title.Category.ToString();
        return category switch
        {
            "Difficulty" => TitleSection.Progression,
            "Skill" => TitleSection.Skill,
            "CoOp" or "CO-OP" => TitleSection.CoOp,
            "Plates" => TitleSection.Plates,
            "Boss Breaker" => TitleSection.BossBreakers,
            "Step Artist" => TitleSection.StepArtists,
            "Play Count" => TitleSection.PlayCount,
            // Phoenix 2 gives each skill track its own category, so an unmapped category that
            // still carries a rail is one of those. Anything with no rail is a one-off, which
            // is the honest default: a progression we cannot name is one we cannot draw.
            _ => title.Ladder != null ? TitleSection.Skill : TitleSection.OneOffs
        };
    }

    public static IReadOnlyList<TitleSectionRows> Build(IEnumerable<TitleProgress> progress, TitleRarityRecord rarity)
    {
        var rungs = progress.Select(p => Rung(p, rarity)).ToArray();

        return Enum.GetValues<TitleSection>()
            .Select(section =>
            {
                var mine = rungs.Where(r => SectionFor(r.Title) == section).ToArray();
                var rails = mine.Where(r => r.Title.Ladder != null)
                    .GroupBy(r => r.Title.Ladder!.Value)
                    .Select(g => new TitleRailRow(g.Key, g.OrderBy(r => r.Rung).ToArray()))
                    .ToArray();
                var oneOffs = mine.Where(r => r.Title.Ladder == null).ToArray();
                return new TitleSectionRows(section, rails, oneOffs);
            })
            .Where(s => s.Total > 0)
            .ToArray();
    }

    /// <summary>
    ///     The title a player wears: the furthest rung they have earned on a progression rail.
    ///     <para>
    ///         Requirement order is NOT progression order and never has been — Expert Lv.2 asks
    ///         80,000 on the 23s while Lv.5 asks 20,000 on the 25s, so the bigger number is the
    ///         easier title. Phoenix folders rank by level then by rating, matching what
    ///         TitleSaga already writes as your highest difficulty title. Phoenix 2 has no
    ///         levels: its pools do rise monotonically, and the merged [P.B] ladder is the one
    ///         you wear, with the per-type ladders standing in only until you hold a rung of it.
    ///     </para>
    /// </summary>
    public static TitleRung? WornTitle(IEnumerable<TitleSectionRows> sections)
    {
        var progression = sections.FirstOrDefault(s => s.Section == TitleSection.Progression);
        if (progression == null) return null;

        TitleRung? Furthest(IEnumerable<TitleRailRow> rails)
        {
            return rails.SelectMany(r => r.Rungs)
                .Where(r => r.State == RungState.Earned)
                .OrderByDescending(r => r.Title is PhoenixDifficultyTitle d ? (int)d.Level : 0)
                .ThenByDescending(r => r.Title.CompletionRequired)
                .FirstOrDefault();
        }

        var merged = progression.Rails.Where(r => r.Name == MergedPoolRail).ToArray();
        return Furthest(merged) ?? Furthest(progression.Rails.Where(r => r.Name != MergedPoolRail));
    }

    private static readonly Name MergedPoolRail = "[P.B]";

    private static TitleRung Rung(TitleProgress progress, TitleRarityRecord rarity)
    {
        // No requirement means no formula: the official import is the only thing that can
        // award it, so it is held or not and never partly done.
        var official = progress.Title.CompletionRequired <= 0;
        var fraction = official ? 0 : progress.PercentComplete;

        var state = progress.IsComplete ? RungState.Earned
            : official || fraction <= 0 ? RungState.Locked
            : RungState.Active;

        var share = rarity.ShareOf(progress.Title.Name);
        // Rarity reads as a percentile of the people who do NOT hold it: 8 holders in 1,562
        // puts you above 99.5% of players. That keeps the one shipped ramp rather than
        // inventing a second, inverted set of cutoffs for this page.
        return new TitleRung(progress, state, official, fraction, share, ThemeScales.BandFor(1 - share));
    }
}
