using System.Diagnostics.CodeAnalysis;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Messaging;

namespace ScoreTracker.Catalog.Contracts.Queries;

/// <summary>
///     Distinct step artists in a mix's catalog with their chart counts — the step-artist
///     autocomplete's dictionary. Since /StepArtists retired into this page, this is the
///     site's only step-artist vocabulary, so the counts are the whole sense of scale.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetSearchStepArtistsQuery(MixEnum Mix)
    : IQuery<IReadOnlyList<ChartSearchVocabularyEntry>>;
