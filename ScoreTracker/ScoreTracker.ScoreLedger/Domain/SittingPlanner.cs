namespace ScoreTracker.ScoreLedger.Domain;

/// <summary>A sitting still taking plays, and the span of play times it holds.</summary>
internal sealed record OpenSitting(Guid Id, DateTimeOffset FirstPlayedAt, DateTimeOffset LastPlayedAt);

/// <summary>A sitting the plan starts, with the play time of its first play.</summary>
internal sealed record NewSitting(Guid Id, DateTimeOffset FirstPlayedAt);

/// <summary>The sitting each play belongs to, in the order the plays were given, and the ones it starts.</summary>
internal sealed record SittingPlan(IReadOnlyList<Guid> SittingIds, IReadOnlyList<NewSitting> Opened);

/// <summary>
///     Sorts plays into sittings by when they were played. The new plays and the player's open
///     sittings are laid out on one timeline and chained wherever each is within <c>gap</c> of the
///     next: a chain holding one open sitting gives it every play in the chain, so a sitting grows in
///     both directions; a chain holding none starts a new sitting; a play that bridges two open
///     sittings joins the nearer, since sittings already written are never merged. Reading play time
///     rather than arrival time is what lets a backlog sent late land in the sittings it was played
///     in, beside a sitting still being played live.
/// </summary>
internal static class SittingPlanner
{
    private sealed record Item(DateTimeOffset Start, DateTimeOffset End, OpenSitting? Sitting, int Play);

    public static SittingPlan Plan(IReadOnlyList<OpenSitting> open, IReadOnlyList<DateTimeOffset> playedAt,
        TimeSpan gap)
    {
        var sittingIds = new Guid[playedAt.Count];
        var opened = new List<NewSitting>();
        var timeline = open.Select(s => new Item(s.FirstPlayedAt, s.LastPlayedAt, s, -1))
            .Concat(playedAt.Select((time, index) => new Item(time, time, null, index)))
            .OrderBy(item => item.Start)
            .ThenBy(item => item.Play);

        var chain = new List<Item>();
        var chainEnd = DateTimeOffset.MinValue;
        foreach (var item in timeline)
        {
            if (chain.Count > 0 && item.Start - chainEnd > gap)
            {
                Assign(chain, sittingIds, opened);
                chain.Clear();
            }

            chain.Add(item);
            if (item.End > chainEnd) chainEnd = item.End;
        }

        if (chain.Count > 0) Assign(chain, sittingIds, opened);
        return new SittingPlan(sittingIds, opened);
    }

    private static void Assign(IReadOnlyList<Item> chain, Guid[] sittingIds, List<NewSitting> opened)
    {
        var plays = chain.Where(item => item.Sitting == null).ToArray();
        if (plays.Length == 0) return;
        var sittings = chain.Where(item => item.Sitting != null).Select(item => item.Sitting!).ToArray();

        if (sittings.Length == 0)
        {
            var sitting = new NewSitting(Guid.NewGuid(), plays.Min(play => play.Start));
            opened.Add(sitting);
            foreach (var play in plays) sittingIds[play.Play] = sitting.Id;
            return;
        }

        foreach (var play in plays)
            sittingIds[play.Play] = sittings.MinBy(sitting => Distance(play.Start, sitting))!.Id;
    }

    private static TimeSpan Distance(DateTimeOffset time, OpenSitting sitting)
    {
        if (time < sitting.FirstPlayedAt) return sitting.FirstPlayedAt - time;
        return time > sitting.LastPlayedAt ? time - sitting.LastPlayedAt : TimeSpan.Zero;
    }
}
