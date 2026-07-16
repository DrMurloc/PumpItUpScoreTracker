using ScoreTracker.Domain.Records;
using ScoreTracker.SharedKernel.Models;

namespace ScoreTracker.OfficialMirror.Domain;

/// <summary>One player's value on one rating board (P1 per-level lists, P2 PUMBILITY tabs).</summary>
internal sealed record RatingBoardEntry(string BoardName, string Username, decimal Value);

/// <summary>
///     One chart board's sweep result, streamed as the scrape walks the song list so the
///     run can checkpoint per board. A null Chart or a SkipReason means the board
///     contributed nothing this snapshot — counted, never fatal.
/// </summary>
internal sealed record OfficialChartBoardResult(int BoardIndex, int BoardsTotal, Chart? Chart,
    string? SkipReason, IReadOnlyList<OfficialChartLeaderboardEntry> Entries);

internal static class Placements
{
    /// <summary>
    ///     Olympic placement: tied values share the place, the next place skips the tie
    ///     block (1, 1, 3). Input order within a tie is preserved.
    /// </summary>
    public static IEnumerable<(T Item, int Place)> Olympic<T>(IEnumerable<T> items, Func<T, decimal> value)
    {
        var place = 1;
        foreach (var group in items.GroupBy(value).OrderByDescending(g => g.Key))
        {
            var groupPlace = place;
            foreach (var item in group)
            {
                yield return (item, groupPlace);
                place++;
            }
        }
    }
}
