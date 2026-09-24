namespace ScoreTracker.ScoreLedger.Domain;

/// <summary>The player's sitting still taking plays, and the span of play times it holds.</summary>
internal sealed record OpenSitting(Guid Id, DateTimeOffset FirstPlayedAt, DateTimeOffset LastPlayedAt);

/// <summary>A sitting the plan starts, with the play time of its first play.</summary>
internal sealed record NewSitting(Guid Id, DateTimeOffset FirstPlayedAt);

/// <summary>The sitting each play belongs to, in the order the plays were given, and the ones it starts.</summary>
internal sealed record SittingPlan(IReadOnlyList<Guid> SittingIds, IReadOnlyList<NewSitting> Opened);

/// <summary>
///     Sorts plays into sittings by when they were played. A play joins the open sitting when it
///     falls within <c>gap</c> of that sitting's span of plays; the rest, taken in play order, start a
///     new sitting wherever two consecutive plays are more than <c>gap</c> apart. Reading play time
///     rather than arrival time is what lets a backlog sent late land in the sittings it was
///     played in.
/// </summary>
internal static class SittingPlanner
{
    public static SittingPlan Plan(OpenSitting? open, IReadOnlyList<DateTimeOffset> playedAt, TimeSpan gap)
    {
        var sittingIds = new Guid[playedAt.Count];
        var opened = new List<NewSitting>();
        var openLast = open?.LastPlayedAt;
        NewSitting? current = null;
        var currentLast = DateTimeOffset.MinValue;

        foreach (var index in Enumerable.Range(0, playedAt.Count).OrderBy(i => playedAt[i]))
        {
            var time = playedAt[index];
            if (open != null && time >= open.FirstPlayedAt - gap && time <= openLast!.Value + gap)
            {
                sittingIds[index] = open.Id;
                if (time > openLast.Value) openLast = time;
                continue;
            }

            if (current == null || time - currentLast > gap)
            {
                current = new NewSitting(Guid.NewGuid(), time);
                opened.Add(current);
            }

            sittingIds[index] = current.Id;
            currentLast = time;
        }

        return new SittingPlan(sittingIds, opened);
    }
}
