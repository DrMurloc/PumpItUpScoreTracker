using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Web.Components;

/// <summary>
///     Which difficulty folders a folder selector offers per category — the single source
///     both <see cref="FolderGrid" /> (the level grid) and <see cref="FolderPicker" /> (its
///     stepper + clamp) read, so the two never drift.
///     <para>
///         Singles stop at <see cref="MaxSingleLevel" />: no single chart harder than S26
///         exists yet (owner, 2026-07-21), so the picker never offers an empty S27+ folder.
///         Doubles run to the game's ceiling (<see cref="DifficultyLevel.Max" />); co-op
///         "levels" are player counts 2–5. Bump the singles cap here the day a harder single
///         ships.
///     </para>
/// </summary>
public static class FolderLevels
{
    public const int MaxSingleLevel = 26;

    /// <summary>The inclusive [min, max] level range a picker offers for the folder.</summary>
    public static (int Min, int Max) Range(ChartTypeCategory category) => category switch
    {
        ChartTypeCategory.CoOp => (2, 5),
        ChartTypeCategory.Single => (1, MaxSingleLevel),
        _ => (1, DifficultyLevel.Max)
    };

    /// <summary>The concrete levels a picker lists for the folder, low to high.</summary>
    public static IEnumerable<int> LevelsFor(ChartTypeCategory category)
    {
        var (min, max) = Range(category);
        return Enumerable.Range(min, max - min + 1);
    }
}
