using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.PlayerProgress.Contracts;

/// <summary>
///     One folder's standing for one player: how much of it they have completed, and what they
///     average across the charts they have played. The two are deliberately never multiplied
///     into a single number — docs/design/folder-level-progression.md §1.1 records what that
///     costs. Completion reacts to the folder gaining charts; the grade cannot, because
///     unplayed charts never enter an average of what was played.
///     <para>
///         <see cref="Played" /> counts <em>passed</em> charts — a broken score is a failed run and
///         feeds neither number, the same rule the folder lamps apply. <see cref="Level" /> carries
///         the player count for co-op folders, matching the convention <see cref="Chart.Level" />
///         already uses.
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

    /// <summary>
    ///     Builds a standing from a folder's charts and a player's best attempts. Public because
    ///     the tier-list page already holds both and would rather show live numbers than a stored
    ///     projection that lags its own score list; the stored path goes through the same code.
    ///     Charts with no passed attempt count toward <see cref="Size" /> and nothing else.
    /// </summary>
    public static FolderLevelRecord For(MixEnum mix, ChartType type, DifficultyLevel level,
        IEnumerable<Chart> folderCharts, IReadOnlyDictionary<Guid, int> passedScores)
    {
        var charts = folderCharts as IReadOnlyCollection<Chart> ?? folderCharts.ToArray();
        var scores = charts
            .Select(c => passedScores.TryGetValue(c.Id, out var score) ? score : (int?)null)
            .Where(s => s != null)
            .Select(s => s!.Value)
            .ToArray();

        // Rounded rather than truncated: an average is a display number, and truncation would
        // drop a folder sitting exactly on a grade floor one rung.
        var average = scores.Length == 0
            ? 0
            : (int)Math.Round(scores.Sum(s => (long)s) / (double)scores.Length, MidpointRounding.AwayFromZero);
        return new FolderLevelRecord(mix, type, level, charts.Count, scores.Length, average);
    }

    /// <summary>
    ///     The scores a standing is built from: passed charts only. A broken score is a failed
    ///     run, so it counts toward neither completion nor the average — the same rule the folder
    ///     lamps apply. Every caller goes through here so the two never drift.
    /// </summary>
    public static IReadOnlyDictionary<Guid, int> PassedScores(IEnumerable<RecordedPhoenixScore> bests) =>
        bests
            .Where(b => b.Score != null && !b.IsBroken)
            .GroupBy(b => b.ChartId)
            .ToDictionary(g => g.Key, g => (int)g.Max(b => b.Score!.Value));
}
