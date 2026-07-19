using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Messaging;

namespace ScoreTracker.OfficialMirror.Contracts.Queries;

/// <summary>
///     Playstyle archetypes for a set of site accounts, resolved through their import-stamped
///     mirror links. Accounts with no link or no snapshot presence are absent from the result.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetOfficialPlayerTypesQuery(MixEnum Mix, IReadOnlyCollection<Guid> UserIds)
    : IQuery<IReadOnlyDictionary<Guid, RecapPlayerType>>;
