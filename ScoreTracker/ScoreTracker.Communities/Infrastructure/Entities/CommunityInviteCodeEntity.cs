using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ScoreTracker.Communities.Infrastructure.Entities
{
    [Index(nameof(CommunityId))]
    internal sealed class CommunityInviteCodeEntity
    {
        public Guid CommunityId { get; set; }
        [Key] public Guid InviteCode { get; set; }
        public DateTime? ExpirationDate { get; set; }
    }
}
