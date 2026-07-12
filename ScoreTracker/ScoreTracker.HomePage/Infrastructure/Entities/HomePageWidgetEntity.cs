using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ScoreTracker.HomePage.Infrastructure.Entities;

/// <summary>
///     One widget instance on a page. WidgetType is the registry's stable TypeId (never
///     renamed — it is also the public export/import vocabulary, D19). Ordinal is the
///     auto-flow order (D6: ordered list + spans, not x/y); ConfigJson + ConfigVersion
///     follow the widget lifecycle contract (§2.3: old blobs tolerated or migrated
///     forever, and public via export).
/// </summary>
[Index(nameof(PageId))]
internal sealed class HomePageWidgetEntity
{
    public Guid Id { get; set; }

    public Guid PageId { get; set; }

    [MaxLength(64)] public string WidgetType { get; set; } = string.Empty;

    [MaxLength(64)] public string? Title { get; set; }

    public byte Ordinal { get; set; }

    [MaxLength(8)] public string SizePreset { get; set; } = string.Empty;

    [MaxLength(2000)] public string ConfigJson { get; set; } = string.Empty;

    public int ConfigVersion { get; set; }
}
