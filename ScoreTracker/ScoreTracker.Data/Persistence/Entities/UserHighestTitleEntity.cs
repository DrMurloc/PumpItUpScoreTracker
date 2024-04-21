using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ScoreTracker.Data.Persistence.Entities
{
    [Index(nameof(Level))]
    [Index(nameof(TitleName))]
    public sealed class UserHighestTitleEntity
    {
        [Key] public Guid UserId { get; set; }

        [Required] [MaxLength(64)] public string TitleName { get; set; } = string.Empty;

        public int Level { get; set; } = 10;
    }
}
