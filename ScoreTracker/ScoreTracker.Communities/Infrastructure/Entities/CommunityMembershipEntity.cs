using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ScoreTracker.Communities.Infrastructure.Entities
{
    [Index(nameof(CommunityId), nameof(UserId))]
    internal sealed class CommunityMembershipEntity
    {
        [Key] public Guid Id { get; set; }
        public Guid CommunityId { get; set; }
        public Guid UserId { get; set; }
    }
}
