namespace ScoreTracker.Catalog.Contracts.Messages;

/// <summary>
///     Bus trigger for the weekly piucenter crawl (design doc tier-lists-overhaul §8a):
///     reconcile the alias map against their chart table, fetch analysis for charts
///     missing the current data release, and regenerate the skill tags from the banked
///     metrics. Idempotent and resumable — a killed run picks up where it left off.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record CrawlPiuCenterCommand
{
}
