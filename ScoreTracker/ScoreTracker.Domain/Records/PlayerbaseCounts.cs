namespace ScoreTracker.Domain.Records;

/// <summary>Registered players and distinct countries represented (front-door stats).</summary>
[ExcludeFromCodeCoverage]
public sealed record PlayerbaseCounts(long Players, int Countries);
