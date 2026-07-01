using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.OfficialMirror.Domain
{
    internal interface IPiuTrackerClient
    {
        Task SyncData(Name gameTag, string sid, CancellationToken cancellationToken);
    }
}
