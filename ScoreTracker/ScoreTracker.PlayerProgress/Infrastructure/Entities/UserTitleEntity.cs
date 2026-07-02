using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ScoreTracker.PlayerProgress.Infrastructure.Entities
{
    [Index(nameof(UserId))]
    [Index(nameof(Title))]
    internal sealed class UserTitleEntity
    {
        [Key] public Guid Id { get; set; }
        public Guid UserId { get; set; }
        [Required] public string Title { get; set; }
        [MaxLength(16)] public string ParagonLevel { get; set; }
    }
}
