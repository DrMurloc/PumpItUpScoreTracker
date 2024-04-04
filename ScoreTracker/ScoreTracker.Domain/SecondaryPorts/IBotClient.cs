namespace ScoreTracker.Domain.SecondaryPorts
{
    public interface IBotClient : IDisposable
    {
        Task PublishQualifiersMessage(string message, CancellationToken cancellationToken);
        public Task Start(CancellationToken cancellationToken = default);
        public Task Stop(CancellationToken cancellationToken = default);

        public Task SendMessageToUser(ulong userId, string message, CancellationToken cancellationToken = default);

        public Task SendFileToUser(ulong userId, Stream fileStream, string fileName, string? message = null,
            CancellationToken cancellationToken = default);

        public Task SendMessage(string message, ulong channelId, CancellationToken cancellationToken = default)
        {
            return SendMessages(new[] { message }, new[] { channelId }, cancellationToken);
        }

        public Task SendMessages(IEnumerable<string> messages, IEnumerable<ulong> channelIds,
            CancellationToken cancellationToken = default);

        public Task RegisterMenuSlashCommand(string name, string description, string response,
            IEnumerable<(string label, string url)> menuButtons);

        public Task RegisterSlashCommand(string name, string description, string response,
            Func<ulong, Task> execution);

        public Task RegisterSlashCommand(string name, string description, string response,
            Func<ulong, ulong, IDictionary<string, string>, Task> execution,
            IEnumerable<(string name, string description)> options);

        public void RegisterReactAdded(Func<string, ulong, ulong, Task> execution);
        public void RegisterReactRemoved(Func<string, ulong, ulong, Task> execution);
        public void WhenReady(Func<Task> execution);
    }
}
