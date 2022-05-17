using System.ComponentModel.DataAnnotations;

namespace ScoreTracker.Data.Persistence.Entities;

public sealed class ChartVideoEntity
{
    [Key] public Guid ChartId { get; set; }

    [Required] [MaxLength(64)] public string VideoUrl { get; set; }

    [Required] [MaxLength(30)] public string ChannelName { get; set; }
}