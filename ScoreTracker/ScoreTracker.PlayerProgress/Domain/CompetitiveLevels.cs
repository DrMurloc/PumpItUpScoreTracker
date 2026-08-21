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
    ///     <para>
    ///         Co-Op has no competitive discipline of its own, so it reads the overall level —
    ///         which on Phoenix and Phoenix 2 excludes co-op folders from debuts ENTIRELY, and is
    ///         meant to (owner, 2026-08-21). A mainline co-op chart has no difficulty: its
    ///         <see cref="SharedKernel.Models.Chart.Level" /> slot holds the PLAYER COUNT, 2 to 5
    ///         (<see cref="SharedKernel.Models.Chart.PlayerCount" />), so the comparison is 2
    ///         against a real overall level of 15–25 and no co-op folder ever clears it. That is
    ///         the wanted answer rather than an accident of the units: "first ever pass in the
    ///         CoOp3 folder" announces a party size, not an achievement, and the folder it names
    ///         is one a player of any level walks into. A co-op pass still earns every other
    ///         flag — folder completion is deliberately un-floored and fires normally.
    ///     </para>
    /// </summary>
    public static int Floor(ChartType type, PlayerStatsRecord stats) =>
        (int)Math.Floor(type switch
        {
            ChartType.Single => stats.SinglesCompetitiveLevel,
            ChartType.Double => stats.DoublesCompetitiveLevel,
            _ => stats.CompetitiveLevel
        });
}
