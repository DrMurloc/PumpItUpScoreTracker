using ScoreTracker.Catalog.Contracts;
using ScoreTracker.SharedKernel.Messaging;

namespace ScoreTracker.Catalog.Contracts.Queries;

/// <summary>Every playable mix, oldest first. No filter — thirty rows is the whole answer.</summary>
[ExcludeFromCodeCoverage]
public sealed record GetMixesQuery : IQuery<IReadOnlyList<MixRecord>>;
