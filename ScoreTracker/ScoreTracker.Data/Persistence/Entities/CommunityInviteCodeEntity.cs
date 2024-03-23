using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ScoreTracker.Data.Persistence.Entities
{
    [Index(nameof(CommunityId))]
    public sealed class CommunityInviteCodeEntity
    {
        public Guid CommunityId { get; set; }
        [Key] public Guid InviteCode { get; set; }
        public DateTime? ExpirationDate { get; set; }
    }
}
