using ScoreTracker.Domain.Records;

namespace ScoreTracker.Domain.SecondaryPorts;

/// <summary>
///     Renders a tier-list share card to a PNG. Implementation is SkiaSharp in the Data
///     layer (cross-platform — System.Drawing retires with it, design doc §7).
/// </summary>
public interface IShareCardRenderer
{
    Task<byte[]> RenderTierListCard(TierListShareCard card, CancellationToken cancellationToken = default);
}
