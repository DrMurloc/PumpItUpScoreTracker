using ScoreTracker.Domain.Records;
using ScoreTracker.PlayerProgress.Contracts.Recap;
using ScoreTracker.SharedKernel.Models;

namespace ScoreTracker.PlayerProgress.Domain.Recap;

/// <summary>
///     Weekly-board recap math over the full placing history. Placements are re-ranked
///     among WITHIN-RANGE entrants only (the stored overall Place mixes ranges); streaks
///     count consecutive ROTATIONS — indexed against the weeks that actually ran — so a
///     skipped rotation breaks nobody's streak unfairly.
/// </summary>
internal static class WeeklyRecapCalculator
{
    /// <summary>Giant Slayer: you outscored someone at least this many competitive levels above you.</summary>
    public const double GiantSlayerLevelGap = 1.0;

    public static RecapWeekly? Calculate(Guid userId, IReadOnlyList<WeeklyPlacingRow> allRows,
        IReadOnlyDictionary<Guid, Chart> charts, IReadOnlyDictionary<Guid, string> userNames)
    {
        var mine = allRows.Where(r => r.UserId == userId).ToArray();
        if (mine.Length == 0) return null;

        var rotationIndex = allRows
            .Select(r => WeekStart(r.ObtainedDate))
            .Distinct()
            .OrderBy(w => w)
            .Select((week, index) => (week, index))
            .ToDictionary(x => x.week, x => x.index);
        var myRotations = mine.Select(r => rotationIndex[WeekStart(r.ObtainedDate)])
            .Distinct()
            .OrderBy(i => i)
            .ToArray();
        var longestStreak = LongestConsecutiveRun(myRotations);

        var placements = new List<(int Rank, int Of, Guid ChartId, DateTime Week)>();
        var giants = new List<(Guid ChartId, Guid GiantId, double Gap, int Margin)>();
        foreach (var group in allRows.GroupBy(r => (Week: WeekStart(r.ObtainedDate), r.ChartId)))
        {
            var my = group.FirstOrDefault(r => r.UserId == userId);
            if (my == null) continue;

            if (my.WasWithinRange)
            {
                var pool = group.Where(r => r.WasWithinRange).ToArray();
                var rank = 1 + pool.Count(r => r.UserId != userId && r.Score > my.Score);
                placements.Add((rank, pool.Length, my.ChartId, group.Key.Week));
            }

            // Slaying requires a real fight: both runs unbroken — beating someone's
            // stage-break is not a story worth telling.
            if (!my.IsBroken)
                giants.AddRange(group
                    .Where(r => r.UserId != userId && !r.IsBroken &&
                                r.CompetitiveLevel >= my.CompetitiveLevel + GiantSlayerLevelGap &&
                                my.Score > r.Score)
                    .Select(r => (my.ChartId, r.UserId, r.CompetitiveLevel - my.CompetitiveLevel,
                        my.Score - r.Score)));
        }

        // One giant = one story, however many weeks you beat them: dedupe per player,
        // keeping their most dramatic fall (biggest gap, then biggest score margin).
        var distinctGiants = giants
            .GroupBy(g => g.GiantId)
            .Select(g => g.OrderByDescending(x => x.Gap).ThenByDescending(x => x.Margin).First())
            .ToArray();

        var best = placements
            .Where(p => charts.ContainsKey(p.ChartId))
            .OrderBy(p => p.Rank)
            .ThenByDescending(p => p.Of)
            .ThenByDescending(p => p.Week)
            .Select(p =>
            {
                var chart = charts[p.ChartId];
                return new RecapBestWeek(p.Rank, p.Of, chart.Id, chart.Song.Name.ToString(), chart.Type,
                    chart.Level, new DateTimeOffset(p.Week, TimeSpan.Zero));
            })
            .FirstOrDefault();

        var topGiants = distinctGiants
            .Where(g => charts.ContainsKey(g.ChartId))
            .OrderByDescending(g => g.Gap)
            .ThenByDescending(g => g.Margin)
            .Take(3)
            .Select(g =>
            {
                var chart = charts[g.ChartId];
                return new RecapGiantSlayer(chart.Id, chart.Song.Name.ToString(), chart.Type, chart.Level,
                    userNames.GetValueOrDefault(g.GiantId, "another player"), g.Gap);
            })
            .ToArray();

        return new RecapWeekly(
            longestStreak,
            myRotations.Length,
            placements.Count(p => p.Rank <= 3),
            placements.Count(p => p.Rank == 1),
            distinctGiants.Length,
            topGiants,
            best);
    }

    private static DateTime WeekStart(DateTimeOffset date)
    {
        return date.Date.AddDays(-(int)date.Date.DayOfWeek);
    }

    private static int LongestConsecutiveRun(IReadOnlyList<int> ascendingDistinct)
    {
        var longest = 0;
        var run = 0;
        for (var i = 0; i < ascendingDistinct.Count; i++)
        {
            run = i > 0 && ascendingDistinct[i] == ascendingDistinct[i - 1] + 1 ? run + 1 : 1;
            longest = Math.Max(longest, run);
        }

        return longest;
    }
}
