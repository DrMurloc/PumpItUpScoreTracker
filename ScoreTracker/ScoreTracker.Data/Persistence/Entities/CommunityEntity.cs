using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ScoreTracker.Data.Persistence.Entities
{
    [Index(nameof(Name))]
    public sealed class CommunityEntity
    {
        [Key] public Guid Id { get; set; }
        [Required] public string Name { get; set; } = string.Empty;
        [Required] public Guid OwningUserId { get; set; }
        [Required] public string PrivacyType { get; set; } = string.Empty;
        public bool IsRegional { get; set; }
    }
}
