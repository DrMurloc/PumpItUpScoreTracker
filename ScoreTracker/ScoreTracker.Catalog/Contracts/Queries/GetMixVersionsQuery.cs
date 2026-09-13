using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Catalog.Contracts.Queries;

/// <summary>
///     The mix's patches, oldest first, each with how many charts first appeared in it. Thirty
///     rows at most; a mix nobody has versioned answers with an empty list.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetMixVersionsQuery(MixEnum Mix) : IQuery<IReadOnlyList<MixVersionRecord>>;
