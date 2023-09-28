namespace ScoreTracker.Domain.SecondaryPorts
{
    public interface IAdminNotificationClient
    {
        Task NotifyAdmin(string message, CancellationToken cancellationToken);
    }
}