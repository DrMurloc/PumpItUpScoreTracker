using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ScoreTracker.Ucs.Infrastructure.Entities;

[Index(nameof(ChartId))]
[Index(nameof(UserId))]
internal sealed class UcsChartTagEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    public Guid ChartId { get; set; }
    public Guid UserId { get; set; }
    [MaxLength(32)] public string Tag { get; set; } = string.Empty;
}
