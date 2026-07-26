using System.Diagnostics.CodeAnalysis;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Messaging;

namespace ScoreTracker.Catalog.Contracts.Queries;

/// <summary>
///     Distinct song artists in a mix's catalog with how many charts each has — the artist
///     autocomplete's dictionary. The count rides along because a name alone gives no sense of
///     whether picking it narrows to three charts or three hundred.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetSearchArtistsQuery(MixEnum Mix)
    : IQuery<IReadOnlyList<ChartSearchVocabularyEntry>>;
