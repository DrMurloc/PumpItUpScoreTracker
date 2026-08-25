using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Domain.Models;

/// <summary>
///     A chart's video, plus — when the video is shared by exactly two singles-family charts
///     of the same song — which half this chart plays on and who holds the other half
///     (docs/design/video-sides.md). Both stay null for solo videos, doubles, and pairs
///     whose sides aren't known.
/// </summary>
public sealed record ChartVideoInformation(
    Guid ChartId,
    Uri VideoUrl,
    Name ChannelName,
    VideoSide? Side = null,
    Guid? PartnerChartId = null)
{
}
