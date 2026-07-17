using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Domain.SecondaryPorts
{
    /// <summary>
    ///     Published read of the Discord broadcast-feed subscriptions (stored by Communities),
    ///     so a vertical that produces a feed's content can fan out to the subscribed channels
    ///     without referencing Communities. Feed kinds are the <see cref="DiscordFeedKinds" />
    ///     constants (the Communities feed-kind names).
    /// </summary>
    public interface IDiscordFeedReader
    {
        Task<IReadOnlyList<ulong>> GetSubscribedChannels(string feedKind, MixEnum mix,
            CancellationToken cancellationToken);
    }

    public static class DiscordFeedKinds
    {
        public const string WeeklyCharts = "WeeklyCharts";
        public const string DailyStep = "DailyStep";
        public const string OfficialLeaderboards = "OfficialLeaderboards";
    }
}
