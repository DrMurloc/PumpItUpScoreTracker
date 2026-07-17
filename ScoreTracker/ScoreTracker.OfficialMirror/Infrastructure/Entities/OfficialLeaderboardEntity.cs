using System.ComponentModel.DataAnnotations;

namespace ScoreTracker.OfficialMirror.Infrastructure.Entities;

/// <summary>
///     Dimension row for one mirrored board — a per-chart score board or a rating board
///     (Phoenix per-level lists, Phoenix 2 PUMBILITY tabs). Chart boards carry their
///     catalog chart so nothing downstream re-parses the display name.
/// </summary>
internal sealed class OfficialLeaderboardEntity
{
    [Key] public int Id { get; set; }
    public Guid MixId { get; set; }
    [MaxLength(20)] public string LeaderboardType { get; set; } = string.Empty;
    [MaxLength(200)] public string Name { get; set; } = string.Empty;
    public Guid? ChartId { get; set; }
    [MaxLength(20)] public string? ChartType { get; set; }
    public int? Level { get; set; }
}
