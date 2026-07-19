using System.Diagnostics.CodeAnalysis;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Messaging;

namespace ScoreTracker.Catalog.Contracts.Queries;

/// <summary>Distinct song artists in a mix's catalog — the artist autocomplete's dictionary.</summary>
[ExcludeFromCodeCoverage]
public sealed record GetSearchArtistsQuery(MixEnum Mix, bool AllMixes = false)
    : IQuery<IReadOnlyList<string>>;
