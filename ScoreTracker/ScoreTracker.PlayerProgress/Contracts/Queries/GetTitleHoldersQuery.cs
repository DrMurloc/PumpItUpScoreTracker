using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.PlayerProgress.Contracts.Queries;

/// <summary>
///     Who holds one title, for the detail drawer. Read on demand rather than with the page:
///     the list is 213 titles deep in Phoenix and 272 in Phoenix 2, and a player opens one.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetTitleHoldersQuery(MixEnum Mix, Name Title) : IQuery<TitleHoldersRecord>;
