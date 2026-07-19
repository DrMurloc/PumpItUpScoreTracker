using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ScoreTracker.Communities.Infrastructure.Entities;

[Index(nameof(CommunityId))]
internal sealed class CommunityChannelEntity
{
    [Key] public Guid Id { get; set; }
    public Guid CommunityId { get; set; }

    public ulong ChannelId { get; set; }

    // The language this channel's community cards render in; null = English.
    [MaxLength(16)] public string? Culture { get; set; }
}