using ScoreTracker.SharedKernel.Enums;

using ScoreTracker.ChartIntelligence.Contracts;

namespace ScoreTracker.ChartIntelligence.Contracts.Queries;

/// <summary>
///     Which folders the PUMBILITY lens has an answer for — everyone's pools when Personalized
///     is false, the reader's own peer group when it is. Drives the folder picker's disabled entries
///     and the redirect that sends a direct URL to the nearest folder with data
///     (docs/design/pumbility-tier-list.md §6).
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetPumbilityFoldersQuery(bool Personalized = false, Guid? UserId = null,
    MixEnum Mix = MixEnum.Phoenix) : IQuery<IReadOnlyList<PumbilityFolderRecord>>
{
}
