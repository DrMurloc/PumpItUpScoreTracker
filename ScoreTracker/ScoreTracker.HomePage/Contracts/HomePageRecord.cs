using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.HomePage.Contracts;

/// <summary>
///     One dashboard page with its widget instances in auto-flow order (D6). DefaultMix
///     is the page-level mix context (D13: widget override → page default → current
///     mix); null = follow the current mix.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record HomePageRecord(Guid Id, Name Name, int Ordinal, bool IsDefault, MixEnum? DefaultMix,
    IReadOnlyList<HomePageWidgetRecord> Widgets)
{
    // D4 caps — enforced in the handlers, raised only on pain points + telemetry.
    public const int MaxPagesPerUser = 8;
    public const int MaxWidgetsPerPage = 8;
    public const int MaxNameLength = 64;
}

/// <summary>
///     One widget instance. WidgetType is the Web registry's stable TypeId; SizePreset
///     is a preset token ("1x1", "2x1", …). ConfigJson + ConfigVersion follow the
///     widget lifecycle contract (§2.3) and are public API via export/import (D19).
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record HomePageWidgetRecord(Guid Id, string WidgetType, string? Title, int Ordinal,
    string SizePreset, string ConfigJson, int ConfigVersion)
{
    public const int MaxTypeLength = 64;
    public const int MaxTitleLength = 64;
    public const int MaxSizeLength = 8;
    public const int MaxConfigLength = 2000;
}
