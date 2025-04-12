using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ScoreTracker.Data.Persistence.Entities
{
    [Index(nameof(ChartId), nameof(LetterGrade), IsUnique = true)]
    public sealed class ChartLetterDifficultyEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public Guid ChartId { get; set; }
        [MaxLength(8)] public string LetterGrade { get; set; } = string.Empty;
        public double Percentile { get; set; }
        public double WeightedSum { get; set; }
    }
}
