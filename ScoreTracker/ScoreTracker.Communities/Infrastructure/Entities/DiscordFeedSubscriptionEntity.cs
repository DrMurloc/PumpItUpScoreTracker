using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ScoreTracker.Communities.Infrastructure.Entities;

// One row per (channel, feed, mix). The unique index makes a repeat registration a no-op.
[Index(nameof(ChannelId), nameof(FeedKind), nameof(Mix), IsUnique = true)]
internal sealed class DiscordFeedSubscriptionEntity
{
    [Key] public Guid Id { get; set; }
    public ulong ChannelId { get; set; }
    [MaxLength(32)] public string FeedKind { get; set; } = string.Empty;
    public int Mix { get; set; }
    public ulong? RegisteredByDiscordUserId { get; set; }
}
