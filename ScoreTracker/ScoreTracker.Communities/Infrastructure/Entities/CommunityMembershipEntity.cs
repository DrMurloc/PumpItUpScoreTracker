using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ScoreTracker.Communities.Infrastructure.Entities
{
    [Index(nameof(CommunityId), nameof(UserId))]
    [Index(nameof(CommunityId), nameof(Role))]
    internal sealed class CommunityMembershipEntity
    {
        [Key] public Guid Id { get; set; }
        public Guid CommunityId { get; set; }
        public Guid UserId { get; set; }

        // Role/permission overlay. Role is a CommunityRole name; Permissions is a CommunityPermission
        // flags int (only meaningful for admins). A Banned row is retained so a lookup blocks rejoin.
        [MaxLength(20)] public string Role { get; set; } = "Member";
        public int Permissions { get; set; }
        public Guid? GrantedByUserId { get; set; }
        public DateTimeOffset? JoinedAt { get; set; }
    }
}
