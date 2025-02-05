using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.SecondaryPorts
{
    public interface IPiuTrackerClient
    {
        Task SyncData(Name gameTag, string sid, CancellationToken cancellationToken);
    }
}
