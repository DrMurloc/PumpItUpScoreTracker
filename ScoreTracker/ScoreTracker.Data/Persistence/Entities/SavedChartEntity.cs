using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ScoreTracker.Data.Persistence.Entities;

[Index(nameof(UserId))]
[Index(nameof(ChartId))]
[Index(nameof(ListName))]
public sealed class SavedChartEntity
{
    [Key] public Guid Id { get; set; }
    [Required] public Guid UserId { get; set; }

    [Required] public Guid ChartId { get; set; }
    [Required] [MaxLength(16)] public string ListName { get; set; }
}