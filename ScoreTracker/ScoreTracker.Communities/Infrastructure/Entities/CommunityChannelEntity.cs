using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ScoreTracker.Communities.Infrastructure.Entities;

[Index(nameof(CommunityId))]
internal sealed class CommunityChannelEntity
{
    [Key] public Guid Id { get; set; }
    public Guid CommunityId { get; set; }

    public ulong ChannelId { get; set; }
    public bool SendNewScores { get; set; }
    public bool SendTitles { get; set; }
    public bool SendNewMembers { get; set; }
}