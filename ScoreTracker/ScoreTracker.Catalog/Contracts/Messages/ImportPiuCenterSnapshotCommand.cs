namespace ScoreTracker.Catalog.Contracts.Messages;

/// <summary>
///     Bus trigger for the piucenter snapshot import: a zipped copy of one data
///     release (per-chart JSONs named "&lt;key&gt;.json", the page-content files, and a
///     version.txt) ingested through the same pipeline as the crawl — alias reconcile,
///     metric banking, skill-tag regeneration — with zero HTTP. Because the banked
///     metrics carry the release version, the weekly crawl afterwards sees everything
///     current and stays a no-op until piucenter ships a new release.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record ImportPiuCenterSnapshotCommand(byte[] SnapshotZip)
{
}
