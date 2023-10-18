namespace ScoreTracker.Domain.SecondaryPorts
{
    public interface IBotClient : IDisposable
    {
        Task PublishQualifiersMessage(string message, CancellationToken cancellationToken);
        public Task Start(CancellationToken cancellationToken = default);
        public Task Stop(CancellationToken cancellationToken = default);
        public void WhenReady(Func<Task> execution);
    }
}
