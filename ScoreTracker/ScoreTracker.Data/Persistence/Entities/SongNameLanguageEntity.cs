using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ScoreTracker.Data.Persistence.Entities
{
    [Index(nameof(CultureCode))]
    public sealed class SongNameLanguageEntity
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [MaxLength(128)] [Required] public string EnglishSongName { get; set; } = string.Empty;
        [MaxLength(8)] [Required] public string CultureCode { get; set; } = string.Empty;

        [MaxLength(128)] [Required] public string SongName { get; set; } = string.Empty;
    }
}
