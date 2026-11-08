using System.Collections.Immutable;
using ScoreTracker.Domain.Models.Titles.Phoenix;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Domain.Models.Titles.Phoenix2;

/// <summary>
///     A title list that is a mix's three PUMBILITY ladders and nothing else — no chart titles, no
///     boss breakers, no folder titles. Both RISE mixes keep their titles against one
///     (docs/design/rise.md D28), each on its own ladders' thresholds (<see cref="PumbilityLadders" />).
/// </summary>
public sealed class PumbilityLadderTitleList : IPhoenixTitleList
{
    private readonly IReadOnlyDictionary<Name, PhoenixTitle> _lookup;
    private readonly MixEnum _mix;
    private readonly IReadOnlyList<Phoenix2PumbilityTitle> _titles;

    public PumbilityLadderTitleList(MixEnum mix)
    {
        _mix = mix;
        _titles = PumbilityLadders.For(mix);
        _lookup = _titles.ToDictionary(t => t.Name, t => (PhoenixTitle)t);
    }

    public IEnumerable<PhoenixTitle> BuildList()
    {
        return _titles.ToArray();
    }

    public PhoenixTitle GetTitleByName(Name name)
    {
        return _lookup[name];
    }

    public IEnumerable<PhoenixTitleProgress> BuildProgress(IDictionary<Guid, Chart> charts,
        IEnumerable<RecordedPhoenixScore> attempts, ISet<Name> completedTitles)
    {
        var progress = _titles.Select(t => new PhoenixTitleProgress(t)).ToImmutableArray();
        PumbilityLadderProgress.Apply(_mix, progress, charts, attempts);
        foreach (var title in progress)
            if (completedTitles.Contains(title.Title.Name))
                title.Complete();
        return progress;
    }
}
