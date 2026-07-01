using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ScoreTracker.OfficialMirror.Infrastructure.Entities
{
    [Index(nameof(UserName))]
    internal sealed class OfficialUserAvatarEntity
    {
        [Key] public Guid Id { get; set; }
        [MaxLength(64)] public string UserName { get; set; }
        [Required] public string AvatarUrl { get; set; } = string.Empty;
    }
}
