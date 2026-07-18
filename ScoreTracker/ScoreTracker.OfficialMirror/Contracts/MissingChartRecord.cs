namespace ScoreTracker.OfficialMirror.Contracts;

/// <summary>One unmapped scraped chart, named exactly as the site serves it.</summary>
[ExcludeFromCodeCoverage]
public sealed record MissingChartRecord(int Id, string SongName, string ChartType, int? Level,
    DateTimeOffset FirstIdentified, DateTimeOffset LastIdentified);
