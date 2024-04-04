using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ScoreTracker.Data.Persistence.Entities;

[Index(nameof(CommunityId))]
public sealed class CommunityChannelEntity
{
    [Key] public Guid Id { get; set; }
    public Guid CommunityId { get; set; }

    public ulong ChannelId { get; set; }
    public bool SendNewScores { get; set; }
    public bool SendTitles { get; set; }
    public bool SendNewMembers { get; set; }
}