using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Messaging;

namespace ScoreTracker.OfficialMirror.Contracts.Queries;

/// <summary>
///     <see cref="GetLinkedOfficialPlayerTagQuery" /> for a set of accounts at once — a page of
///     API rows that each carry a game tag, where a query per row would put a hundred round
///     trips behind one response. Accounts with no link in the mix are absent from the result.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetLinkedOfficialPlayerTagsQuery(MixEnum Mix, IReadOnlyCollection<Guid> UserIds)
    : IQuery<IReadOnlyDictionary<Guid, string>>;
