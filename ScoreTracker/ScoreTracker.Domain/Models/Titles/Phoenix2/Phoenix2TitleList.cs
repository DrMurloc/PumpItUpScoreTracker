using System.Collections.Immutable;
using ScoreTracker.Domain.Models.Titles.Phoenix;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Domain.Models.Titles.Phoenix2;

/// <summary>
///     Phoenix 2's title list, mirroring <see cref="PhoenixTitleList" />'s shape but deliberately
///     EMPTY (locked decision, plan doc): the game launches with no known titles — including
///     difficulty titles — so progress is always empty and no title can ever complete until the
///     owner verifies the real Phoenix 2 title list and populates this.
/// </summary>
public static class Phoenix2TitleList
{
    private static readonly PhoenixTitle[] Titles = Array.Empty<PhoenixTitle>();

    private static readonly IDictionary<Name, PhoenixTitle> TitleLookup = Titles.ToDictionary(n => n.Name);

    public static PhoenixTitle GetTitleByName(Name name)
    {
        return TitleLookup[name];
    }

    public static IEnumerable<PhoenixTitle> BuildList()
    {
        return Titles.ToArray();
    }

    public static IEnumerable<PhoenixTitleProgress> BuildProgress(IDictionary<Guid, Chart> charts,
        IEnumerable<RecordedPhoenixScore> attempts,
        ISet<Name> completedTitles)
    {
        var progress = Titles.Select(t => new PhoenixTitleProgress(t)).ToImmutableArray();
        foreach (var attempt in attempts)
        foreach (var title in progress)
        {
            title.ApplyAttempt(charts[attempt.ChartId], attempt);
            if (completedTitles.Contains(title.Title.Name))
                title.Complete();
        }

        return progress;
    }
}
