namespace ScoreTracker.Catalog.Contracts;

/// <summary>An external key the crawler couldn't auto-match — the admin grid's work queue.</summary>
[ExcludeFromCodeCoverage]
public sealed record UnresolvedAliasRecord(string ExternalKey, DateTimeOffset LastCheckedAt);
