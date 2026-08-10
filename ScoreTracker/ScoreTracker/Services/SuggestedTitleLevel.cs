using ScoreTracker.Domain.Models.Titles;
using ScoreTracker.Domain.Models.Titles.Phoenix2;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Web.Services;

/// <param name="Folders">"S19", or both types for a merged-pool title.</param>
/// <param name="Grade">
///     The lowest grade this rung answers for. With <paramref name="OrBetter" /> set it stands for
///     a run of grades that all land on the same folder.
/// </param>
/// <param name="OrBetter">True when this rung absorbed the grades above it.</param>
/// <param name="Reachable">
///     False when no folder on the curve reaches the threshold at this grade. The folders then name
///     the ceiling that falls short rather than one that serves.
/// </param>
public sealed record SuggestedRung(
    IReadOnlyList<string> Folders,
    PhoenixLetterGrade Grade,
    bool OrBetter,
    bool Reachable);

/// <param name="Rungs">One per reference grade, best first, runs of equal folders merged.</param>
/// <param name="Plate">The reference plate every rung assumes.</param>
public sealed record SuggestedLevel(IReadOnlyList<SuggestedRung> Rungs, PhoenixPlate Plate);

/// <summary>
///     Where a Phoenix 2 PUMBILITY title sits on the folder ladder — the tier-list page's
///     "serves" read pointed forward: the lowest folder whose fifty charts reach the title's
///     threshold.
///     <para>
///         Deliberately NOT personalised. The tier-list track answers "what does this folder do
///         for me" and needs your pool, your floor and your median to do it; this answers "which
///         folder is this title", which is a property of the title. Fixed reference performances
///         keep the answer stable, comparable between titles, and true for a player who has
///         imported nothing.
///     </para>
///     <para>
///         One folder is the wrong shape for that answer, because how well you play moves it by up
///         to eight levels. So it answers at three grades instead, and the middle one is the AAA
///         this used to print alone.
///     </para>
/// </summary>
public static class SuggestedTitleLevel
{
    /// <summary>
    ///     Best first, so the levels ascend down the block and the grades read in the order every
    ///     other grade list on the site uses. A is the lowest grade whose multiplier is verified
    ///     against live data — below it the shipped config is still extrapolating.
    /// </summary>
    private static readonly PhoenixLetterGrade[] ReferenceGrades =
    {
        PhoenixLetterGrade.SSSPlus,
        PhoenixLetterGrade.AAA,
        PhoenixLetterGrade.A
    };

    private const PhoenixPlate ReferencePlate = PhoenixPlate.TalentedGame;

    private const int PoolSize = 50;

    /// <summary>Below level 10 a Phoenix 2 chart prices at zero, so no folder there ever serves.</summary>
    private const int LowestScoringLevel = 10;

    public static SuggestedLevel? For(Title title)
    {
        if (title is not Phoenix2PumbilityTitle pumbility) return null;

        var config = ScoringConfiguration.PumbilityScoring(MixEnum.Phoenix2, false);

        var types = pumbility.Pool switch
        {
            PumbilityPool.Singles => new[] { ChartType.Single },
            PumbilityPool.Doubles => new[] { ChartType.Double },
            // A merged pool can be filled from either side, so it names both.
            _ => new[] { ChartType.Single, ChartType.Double }
        };

        var rungs = ReferenceGrades
            .Select(grade => RungFor(config, types, title.CompletionRequired, grade))
            .ToArray();

        // Nothing on the curve serves at any reference grade. The drawer drops the line rather
        // than printing three ways to fall short; SuggestedTitleLevelTests is the tripwire.
        if (rungs.All(r => !r.Reachable)) return null;

        return new SuggestedLevel(Merge(rungs), ReferencePlate);
    }

    private static SuggestedRung RungFor(
        ScoringConfiguration config,
        IReadOnlyList<ChartType> types,
        int threshold,
        PhoenixLetterGrade grade)
    {
        // Priced per type, not once for both: Phoenix 2 gives Singles their own value for some
        // grades and plates, so a merged pool's two folders can sit at different levels.
        var folders = types
            .Select(type => Folder(type, threshold,
                config.LetterGradeModifierFor(grade, type) + config.PlateModifierFor(ReferencePlate, type)))
            .ToArray();

        // Both types cap at the same pool — a singles chart prices one level up but clamps at the
        // ceiling — so either every type serves or none does.
        var reachable = folders.All(f => f != null);
        var named = types
            .Select((type, i) => folders[i] ?? $"{type.GetShortHand()}{(int)DifficultyLevel.Max}")
            .ToArray();

        return new SuggestedRung(named, grade, false, reachable);
    }

    /// <summary>
    ///     Grades that land on the same folder collapse into one rung reading "{lowest} or better".
    ///     Two identical rows read as a rendering fault, and the level-10 floor produces them for
    ///     every easy title. An unreachable rung never merges — its folders name a ceiling, not an
    ///     answer.
    /// </summary>
    private static IReadOnlyList<SuggestedRung> Merge(IReadOnlyList<SuggestedRung> rungs)
    {
        var merged = new List<SuggestedRung>();
        foreach (var rung in rungs)
        {
            var previous = merged.Count == 0 ? null : merged[^1];
            if (previous is { Reachable: true } && rung.Reachable && previous.Folders.SequenceEqual(rung.Folders))
            {
                // Reference grades descend, so this rung's grade is the run's new floor.
                merged[^1] = previous with { Grade = rung.Grade, OrBetter = true };
                continue;
            }

            merged.Add(rung);
        }

        return merged;
    }

    private static string? Folder(ChartType type, int threshold, double perChart)
    {
        for (var level = LowestScoringLevel; level <= (int)DifficultyLevel.Max; level++)
        {
            // Singles price one level up the base curve, so the level a player stands in is
            // one below the level their score is worth.
            var effective = type == ChartType.Single
                ? Math.Min(level + 1, (int)DifficultyLevel.Max)
                : level;
            var pool = PoolSize * ScoringConfiguration.Phoenix2BaseRating(DifficultyLevel.From(effective)) * perChart;
            if (pool >= threshold) return $"{type.GetShortHand()}{level}";
        }

        return null;
    }
}
