using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;

namespace ScoreTracker.Communities.Infrastructure.Entities
{
    // A user holds at most one row per community — member, admin, creator or ban are all the
    // same seat. Unique so a concurrent join cannot double-insert.
    [Index(nameof(CommunityId), nameof(UserId), IsUnique = true)]
    [Index(nameof(CommunityId), nameof(Role))]
    // UserId is whose seat this is. GrantedByUserId is another member entirely, so a purge keyed
    // on it would revoke a surviving admin because whoever promoted them left.
    [PurgeKey(nameof(UserId))]
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
