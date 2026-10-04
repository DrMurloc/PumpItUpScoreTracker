
using ScoreTracker.Domain.Records;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Domain.SecondaryPorts
{
    public interface ITierListRepository
    {
        Task SaveEntry(MixEnum mix, SongTierListEntry entry, CancellationToken cancellationToken);

        Task<IEnumerable<Guid>> GetUsersOnLevel(MixEnum mix, DifficultyLevel level,
            CancellationToken cancellationToken, bool requireActive = false);

        Task<IEnumerable<SongTierListEntry>> GetAllEntries(MixEnum mix, Name tierListName,
            CancellationToken cancellationToken);

        Task SaveEntries(MixEnum mix, IEnumerable<SongTierListEntry> entry, CancellationToken cancellationToken);

        /// <summary>
        ///     Replaces every peer key's rows for one PUMBILITY tier-list folder
        ///     (docs/design/pumbility-tier-list.md §8). Peer keys absent from
        ///     <paramref name="byPeerKey" /> end up with no rows, which is how a peer group says it
        ///     cannot speak for this folder — writing a full set of zeros for every group that
        ///     cannot reach a folder would be most of the table, since a group's pools only
        ///     cover a three-to-four level band.
        /// </summary>
        Task SavePumbilityTierLists(MixEnum mix, ChartType chartType, DifficultyLevel level,
            IReadOnlyDictionary<string, PumbilityTierListFolder> byPeerKey,
            CancellationToken cancellationToken);

        /// <summary>One PUMBILITY tier-list folder, for one peer key ("*" is everyone).</summary>
        Task<PumbilityTierListFolder> GetPumbilityTierList(MixEnum mix, ChartType chartType,
            DifficultyLevel level, string peerKey, CancellationToken cancellationToken);

        /// <summary>
        ///     Which folders the PUMBILITY tier list can answer for a peer key — the ones holding
        ///     at least one chart somebody pools. Drives the folder selector's disabled options
        ///     and the redirect that sends a direct URL to the nearest folder with an answer.
        /// </summary>
        Task<IEnumerable<(ChartType ChartType, int Level)>> GetPumbilityTierListFolders(MixEnum mix,
            string peerKey, CancellationToken cancellationToken);
    }
}
