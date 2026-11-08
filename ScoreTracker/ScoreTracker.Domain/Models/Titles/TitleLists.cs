using ScoreTracker.Domain.Models.Titles.Phoenix;
using ScoreTracker.Domain.Models.Titles.Phoenix2;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Domain.Models.Titles;

/// <summary>
///     Mix-level facts about the shipped title taxonomy, derived from the lists themselves so
///     they can never drift from what actually ships.
/// </summary>
public static class TitleLists
{
    // The TYPE is the test, matching the recommendation engine's own emptiness condition —
    // Phoenix 2's "Difficulty" CATEGORY names its pumbility titles, so a category check would
    // wrongly say P2 has a level ladder to hunt.
    private static readonly bool PhoenixHasDifficulty =
        PhoenixTitleList.BuildList().Any(t => t is PhoenixDifficultyTitle);

    private static readonly bool Phoenix2HasDifficulty =
        Phoenix2TitleList.BuildList().Any(t => t is PhoenixDifficultyTitle);

    private static readonly IPhoenixTitleList PhoenixList = new StaticList(PhoenixTitleList.BuildList,
        PhoenixTitleList.GetTitleByName, PhoenixTitleList.BuildProgress);

    private static readonly IPhoenixTitleList Phoenix2List = new StaticList(Phoenix2TitleList.BuildList,
        Phoenix2TitleList.GetTitleByName, Phoenix2TitleList.BuildProgress);

    private static readonly IPhoenixTitleList RiseList = new PumbilityLadderTitleList(MixEnum.Rise);
    private static readonly IPhoenixTitleList RiseArcadeList = new PumbilityLadderTitleList(MixEnum.RiseArcade);

    /// <summary>
    ///     The title list a mix keeps players' titles against, or null for a mix that keeps none.
    ///     Each mix is named: an unknown mix answers null rather than borrowing another's list.
    ///     XX's list is computed on read and never saved, so it is not one of these.
    /// </summary>
    public static IPhoenixTitleList? For(MixEnum mix)
    {
        return mix switch
        {
            MixEnum.Phoenix => PhoenixList,
            MixEnum.Phoenix2 => Phoenix2List,
            MixEnum.Rise => RiseList,
            MixEnum.RiseArcade => RiseArcadeList,
            _ => null
        };
    }

    /// <summary>
    ///     Whether the mix has chart-level difficulty titles to push toward. Phoenix does;
    ///     Phoenix 2 does not — its 272 titles are pumbility ladders, grade badges and play
    ///     counts. If Andamiro ships P2 difficulty titles and the list gains them, this flips
    ///     on its own.
    /// </summary>
    public static bool HasDifficultyTitles(MixEnum mix)
    {
        return mix switch
        {
            MixEnum.Phoenix => PhoenixHasDifficulty,
            MixEnum.Phoenix2 => Phoenix2HasDifficulty,
            _ => false
        };
    }

    private sealed class StaticList : IPhoenixTitleList
    {
        private readonly Func<IEnumerable<PhoenixTitle>> _list;
        private readonly Func<Name, PhoenixTitle> _byName;

        private readonly Func<IDictionary<Guid, Chart>, IEnumerable<RecordedPhoenixScore>, ISet<Name>,
            IEnumerable<PhoenixTitleProgress>> _progress;

        public StaticList(Func<IEnumerable<PhoenixTitle>> list, Func<Name, PhoenixTitle> byName,
            Func<IDictionary<Guid, Chart>, IEnumerable<RecordedPhoenixScore>, ISet<Name>,
                IEnumerable<PhoenixTitleProgress>> progress)
        {
            _list = list;
            _byName = byName;
            _progress = progress;
        }

        public IEnumerable<PhoenixTitle> BuildList()
        {
            return _list();
        }

        public PhoenixTitle GetTitleByName(Name name)
        {
            return _byName(name);
        }

        public IEnumerable<PhoenixTitleProgress> BuildProgress(IDictionary<Guid, Chart> charts,
            IEnumerable<RecordedPhoenixScore> attempts, ISet<Name> completedTitles)
        {
            return _progress(charts, attempts, completedTitles);
        }
    }
}
