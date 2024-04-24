using System.ComponentModel.DataAnnotations;

namespace ScoreTracker.Data.Persistence.Entities;

public sealed class ChartVideoEntity
{
    [Key] public Guid ChartId { get; set; }

    [Required] [MaxLength(64)] public string VideoUrl { get; set; } = string.Empty;

    [Required] [MaxLength(30)] public string ChannelName { get; set; } = string.Empty;
    public DateTimeOffset? LastUpdated { get; set; }
}