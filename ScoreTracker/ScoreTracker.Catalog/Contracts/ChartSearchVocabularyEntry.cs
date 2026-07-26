namespace ScoreTracker.Catalog.Contracts;

/// <summary>
///     One free-text facet value and how many of the mix's charts carry it. Used by the artist
///     and step-artist autocompletes, where the count is the difference between picking a name
///     blind and knowing it narrows to four charts.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record ChartSearchVocabularyEntry(string Value, int ChartCount);
