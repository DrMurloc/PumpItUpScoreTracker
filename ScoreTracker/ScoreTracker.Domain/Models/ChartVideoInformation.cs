using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Domain.Models;

public sealed record ChartVideoInformation(Guid ChartId, Uri VideoUrl, Name ChannelName)
{
}