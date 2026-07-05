using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ScoreTracker.PlayerProgress.Infrastructure.Entities
{
    [Index(nameof(Level))]
    [Index(nameof(TitleName))]
    [PrimaryKey(nameof(UserId), nameof(MixId))]
    internal sealed class UserHighestTitleEntity
    {
        public Guid UserId { get; set; }

        public Guid MixId { get; set; }

        [Required] [MaxLength(64)] public string TitleName { get; set; } = string.Empty;

        public int Level { get; set; } = 10;
    }
}
