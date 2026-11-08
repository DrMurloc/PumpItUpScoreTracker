using ScoreTracker.Domain.Models.Titles.Phoenix;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Domain.Models.Titles;

/// <summary>
///     A title list the site keeps a player's titles against: what it holds, a title by its name,
///     and a player's progress on every title from their records and the titles already saved.
///     <see cref="TitleLists.For" /> resolves one per mix.
/// </summary>
public interface IPhoenixTitleList
{
    IEnumerable<PhoenixTitle> BuildList();

    PhoenixTitle GetTitleByName(Name name);

    IEnumerable<PhoenixTitleProgress> BuildProgress(IDictionary<Guid, Chart> charts,
        IEnumerable<RecordedPhoenixScore> attempts, ISet<Name> completedTitles);
}
