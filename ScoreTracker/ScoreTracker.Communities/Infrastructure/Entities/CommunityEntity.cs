using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ScoreTracker.Communities.Infrastructure.Entities
{
    [Index(nameof(Name))]
    internal sealed class CommunityEntity
    {
        [Key] public Guid Id { get; set; }
        [Required] public string Name { get; set; } = string.Empty;
        [Required] public Guid OwningUserId { get; set; }
        [Required] public string PrivacyType { get; set; } = string.Empty;
        public bool IsRegional { get; set; }

        // CommunityPermission flags applied to newly promoted admins (creator-settable). Seed =
        // ManageInviteLinks | ManageUsers | ManageChannelSubscriptions = 13.
        public int DefaultAdminPermissions { get; set; } = 13;

        // Fallback culture for this community's Discord notifications (creator-settable).
        [MaxLength(20)] public string? DefaultLanguage { get; set; }
    }
}
