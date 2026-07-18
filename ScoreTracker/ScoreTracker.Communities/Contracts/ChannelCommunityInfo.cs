using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Communities.Contracts
{
    /// <summary>
    ///     A community a Discord channel is registered to, with its regional flag and the
    ///     language the channel's cards render in (null = English).
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed record ChannelCommunityInfo(Name Name, bool IsRegional, string? Culture = null);
}
