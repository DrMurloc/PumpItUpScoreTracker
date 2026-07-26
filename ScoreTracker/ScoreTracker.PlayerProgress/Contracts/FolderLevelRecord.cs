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
    int AverageScore,
    int TierScore = 0)
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
    ///     The grade you hold across the whole tier: read the folder best-first and this is the
    ///     score sitting at the tier's position, so "80% · AAA" means AAA or better on 80% of the
    ///     folder. It is the colour under the tick on the spectrum, which is what makes the bar
    ///     and the letter the same claim.
    ///     <para>
    ///         Completionist first, by design: climbing a tier reaches deeper into the folder and
    ///         can therefore lower the letter. That is the trade the folder is asking you to make,
    ///         and every tier you have already cleared keeps the grade it was earned at.
    ///     </para>
    ///     Null below the first tier — under 20% there is no rung to hold a grade on.
    /// </summary>
    public PhoenixLetterGrade? Grade =>
        Tier <= 0 || TierScore <= 0 ? null : PhoenixScore.From(TierScore).LetterGradeFor(Mix);

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
            .OrderByDescending(s => s)
            .ToArray();

        // Rounded rather than truncated: an average is a display number, and truncation would
        // drop a folder sitting exactly on a grade floor one rung.
        var average = scores.Length == 0
            ? 0
            : (int)Math.Round(scores.Sum(s => (long)s) / (double)scores.Length, MidpointRounding.AwayFromZero);
        return new FolderLevelRecord(mix, type, level, charts.Count, scores.Length, average,
            ScoreAtTier(charts.Count, scores));
    }

    /// <summary>
    ///     The score sitting at the tier's position in the best-first list — the worst score
    ///     inside the tier, and therefore the one the whole tier is held at. Completion is always
    ///     at least the tier, so the position never falls past what has been played.
    /// </summary>
    private static int ScoreAtTier(int size, IReadOnlyList<int> scoresDescending)
    {
        if (size <= 0 || scoresDescending.Count == 0) return 0;
        var tier = FolderCompletionTier.For((int)Math.Floor(100.0 * scoresDescending.Count / size));
        if (tier <= 0) return 0;
        var position = (int)Math.Ceiling(tier / 100.0 * size);
        return scoresDescending[Math.Min(position, scoresDescending.Count) - 1];
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
