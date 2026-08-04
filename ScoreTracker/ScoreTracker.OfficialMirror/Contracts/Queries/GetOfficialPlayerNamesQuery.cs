using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Messaging;

namespace ScoreTracker.OfficialMirror.Contracts.Queries;

/// <summary>
///     Every board tag ever seen for the mix, departed players included — history is searchable,
///     which is what the Players view wants. A caller that needs the tags currently ON the boards,
///     and needs them narrowed by a search term, wants <see cref="SearchOfficialBoardTagsQuery" />.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetOfficialPlayerNamesQuery(MixEnum Mix) : IQuery<IReadOnlyList<string>>;
