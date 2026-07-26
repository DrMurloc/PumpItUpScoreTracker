using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.PlayerProgress.Contracts;

/// <summary>
///     One folder's standing for one player: how much of it they have completed, and what they
///     average across the charts they have played. The two are deliberately never multiplied
///     into a single number — docs/design/folder-level-progression.md §1.1 records what that
///     costs. Completion reacts to the folder gaining charts; the grade cannot, because
///     unplayed charts never enter an average of what was played.
///     <para>
///         <see cref="Level" /> carries the player count for co-op folders, matching the
///         convention <see cref="Chart.Level" /> already uses.
///     </para>
/// </summary>
public sealed record FolderLevelRecord(
    MixEnum Mix,
    ChartType Type,
    DifficultyLevel Level,
    int Size,
    int Played,
    int AverageScore)
{
    /// <summary>The folder key milestones and UI share, e.g. "S22" — the FolderPassLamp vocabulary.</summary>
    public string Folder => $"{Type.GetShortHand()}{(int)Level}";

    /// <summary>
    ///     Completion as a whole percent, floored so that a folder one chart short of complete
    ///     can never round up into reading as a Folder Lamp.
    /// </summary>
    public int CompletionPercent => Size == 0 ? 0 : (int)Math.Floor(100.0 * Played / Size);

    /// <summary>The highest <see cref="FolderCompletionTier" /> reached, or 0 below the first.</summary>
    public int Tier => FolderCompletionTier.For(CompletionPercent);

    public bool IsLamped => Size > 0 && Played >= Size;

    /// <summary>
    ///     Null until at least one chart is played — the average of nothing is not an F, it is
    ///     an absence, and every surface renders it as one.
    /// </summary>
    public PhoenixLetterGrade? Grade =>
        Played <= 0 ? null : PhoenixScore.From(AverageScore).LetterGradeFor(Mix);
}
