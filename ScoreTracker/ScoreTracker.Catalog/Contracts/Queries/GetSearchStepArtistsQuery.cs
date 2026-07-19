using System.Diagnostics.CodeAnalysis;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Messaging;

namespace ScoreTracker.Catalog.Contracts.Queries;

/// <summary>Distinct step artists in a mix's catalog — the step-artist autocomplete's dictionary.</summary>
[ExcludeFromCodeCoverage]
public sealed record GetSearchStepArtistsQuery(MixEnum Mix, bool AllMixes = false)
    : IQuery<IReadOnlyList<string>>;
