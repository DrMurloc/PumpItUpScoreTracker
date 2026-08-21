using ScoreTracker.Domain.Records;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.PlayerProgress.Domain;

/// <summary>
///     The competitive-level bar a (type, level) folder clears before a first pass in it counts
///     as a debut rather than a back-fill.
///     <para>
///         One definition for both surfaces that ask — the 🆕 flag capture writes onto a session
///         row, and the significant-win policy behind the community and rival feeds — so a debut
///         cannot be marked on one and withheld by the other.
///     </para>
/// </summary>
internal static class CompetitiveLevels
{
    /// <summary>
    ///     The player's competitive level for a chart type, floored to a whole difficulty level.
    ///     Co-Op has no competitive discipline of its own, so it reads the overall level.
    /// </summary>
    public static int Floor(ChartType type, PlayerStatsRecord stats) =>
        (int)Math.Floor(type switch
        {
            ChartType.Single => stats.SinglesCompetitiveLevel,
            ChartType.Double => stats.DoublesCompetitiveLevel,
            _ => stats.CompetitiveLevel
        });
}
