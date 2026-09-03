using ScoreTracker.Catalog.Contracts;
using ScoreTracker.SharedKernel.Messaging;

namespace ScoreTracker.Catalog.Contracts.Queries;

/// <summary>
///     Every avatar the official pages list, alphabetical, with the mixes it appears in and each
///     distinct picture of it. Static seeded data — the handler caches it for the process.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetAvatarCatalogQuery : IQuery<IReadOnlyList<AvatarRecord>>;
