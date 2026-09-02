using ScoreTracker.Domain.Records;

namespace ScoreTracker.Domain.SecondaryPorts;

/// <summary>
///     Renders a tier-list share card to a PNG. Implementation is SkiaSharp in the Data
///     layer (cross-platform — System.Drawing retires with it, design doc §7).
/// </summary>
public interface IShareCardRenderer
{
    Task<byte[]> RenderTierListCard(TierListShareCard card, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Warms the renderer's image cache for the given URLs — the slow phase of a cold render
    ///     is fetching sixty jackets, and a page that warms them in small batches can put real
    ///     counts on a progress bar (design doc §8). Idempotent; a cached URL costs nothing.
    /// </summary>
    Task PrefetchImages(IReadOnlyList<string> urls, CancellationToken cancellationToken = default);
}
