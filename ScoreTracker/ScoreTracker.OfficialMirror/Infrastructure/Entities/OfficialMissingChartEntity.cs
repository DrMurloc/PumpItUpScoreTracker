using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ScoreTracker.OfficialMirror.Infrastructure.Entities;

/// <summary>
///     A chart the sweep scraped but could not match to the catalog — one row per distinct
///     identity, LastIdentified refreshed by every sweep that still cannot map it. Rows
///     leave by admin resolution (and simply return on the next sweep if the catalog gap
///     is still real).
/// </summary>
[Index(nameof(MixId), nameof(SongName), nameof(ChartType), nameof(Level), IsUnique = true)]
internal sealed class OfficialMissingChartEntity
{
    [Key] public int Id { get; set; }
    public Guid MixId { get; set; }
    [MaxLength(250)] public string SongName { get; set; } = string.Empty;
    [MaxLength(30)] public string ChartType { get; set; } = string.Empty;
    public int? Level { get; set; }
    public DateTimeOffset FirstIdentified { get; set; }
    public DateTimeOffset LastIdentified { get; set; }
}
