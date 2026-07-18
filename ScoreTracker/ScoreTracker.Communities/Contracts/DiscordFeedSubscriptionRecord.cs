using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Communities.Contracts
{
    /// <summary>
    ///     One channel's subscription to a broadcast feed for a given mix. Culture is the
    ///     language its posts render in (null = English).
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed record DiscordFeedSubscriptionRecord(ulong ChannelId, DiscordFeedKind Kind, MixEnum Mix,
        string? Culture = null);
}
