using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ScoreTracker.Data.Persistence.Entities
{
    [Index(nameof(CommunityId), nameof(UserId))]
    public sealed class CommunityMembershipEntity
    {
        [Key] public Guid Id { get; set; }
        public Guid CommunityId { get; set; }
        public Guid UserId { get; set; }
    }
}
