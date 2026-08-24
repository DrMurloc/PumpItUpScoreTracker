using System.ComponentModel.DataAnnotations;

namespace ScoreTracker.Catalog.Infrastructure.Entities;

internal sealed class ChartVideoEntity
{
    [Key] public Guid ChartId { get; set; }

    [Required] [MaxLength(64)] public string VideoUrl { get; set; } = string.Empty;

    [Required] [MaxLength(30)] public string ChannelName { get; set; } = string.Empty;
    public DateTimeOffset? LastUpdated { get; set; }

    /// <summary>
    ///     "Left"/"Right" when this chart is one half of a two-sided video, null otherwise.
    ///     Maintained by the write paths via <see cref="Domain.VideoSideAssigner" />; the
    ///     Single+Performance pairs carry hand-researched values (docs/design/video-sides.md).
    /// </summary>
    [MaxLength(8)]
    public string? Side { get; set; }
}