using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Communities.Contracts
{
    /// <summary>A community a Discord channel is registered to, with its regional flag.</summary>
    [ExcludeFromCodeCoverage]
    public sealed record ChannelCommunityInfo(Name Name, bool IsRegional);
}
