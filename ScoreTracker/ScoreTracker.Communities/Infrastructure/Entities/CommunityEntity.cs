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
    }
}
