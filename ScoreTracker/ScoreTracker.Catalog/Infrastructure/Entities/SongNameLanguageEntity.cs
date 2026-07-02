using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ScoreTracker.Catalog.Infrastructure.Entities
{
    [Index(nameof(CultureCode))]
    internal sealed class SongNameLanguageEntity
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [MaxLength(128)] [Required] public string EnglishSongName { get; set; } = string.Empty;
        [MaxLength(8)] [Required] public string CultureCode { get; set; } = string.Empty;

        [MaxLength(128)] [Required] public string SongName { get; set; } = string.Empty;
    }
}
