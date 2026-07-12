using MediatR;

namespace ScoreTracker.HomePage.Contracts.Commands;

// Widget-instance mutations. The vertical validates SHAPE (lengths, presence); the
// Web registry owns semantic validation (known TypeId, supported size for that type)
// because widget descriptors live with the render components (D15).

/// <summary>Appends at the end of the page's auto-flow order.</summary>
[ExcludeFromCodeCoverage]
public sealed record AddHomePageWidgetCommand(Guid PageId, string WidgetType, string SizePreset,
    string? Title, string ConfigJson, int ConfigVersion) : IRequest<HomePageWidgetRecord>;

/// <summary>Remaining widgets' ordinals compact.</summary>
[ExcludeFromCodeCoverage]
public sealed record RemoveHomePageWidgetCommand(Guid WidgetId) : IRequest;

/// <summary>
///     The arrows' reorder operation: remove-and-insert at the clamped index —
///     adjacent moves read as swaps, which keeps the mental model unified with drag.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record MoveHomePageWidgetCommand(Guid WidgetId, int NewOrdinal) : IRequest;

/// <summary>
///     Drag's semantic (field-test round 1): on a 2D grid with mixed spans, dropping
///     A on B means "trade places — nobody else moves". Insertion semantics shoved
///     bystander widgets around; swap doesn't. Same-page only.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record SwapHomePageWidgetsCommand(Guid WidgetId, Guid TargetWidgetId) : IRequest;

[ExcludeFromCodeCoverage]
public sealed record ResizeHomePageWidgetCommand(Guid WidgetId, string SizePreset) : IRequest;

/// <summary>Null title falls back to the widget type's localized display name.</summary>
[ExcludeFromCodeCoverage]
public sealed record RenameHomePageWidgetCommand(Guid WidgetId, string? Title) : IRequest;

[ExcludeFromCodeCoverage]
public sealed record UpdateHomePageWidgetConfigCommand(Guid WidgetId, string ConfigJson, int ConfigVersion)
    : IRequest;

/// <summary>
///     The import path (D19): swaps a page's entire widget list in one transaction —
///     the ONE mutation that is not per-interaction (§2.6). Specs are validated for
///     shape here; semantic validation (known types, supported sizes) happened at the
///     Web layer against the registry before dispatch.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record ReplaceHomePageWidgetsCommand(Guid PageId, IReadOnlyList<HomePageWidgetSpec> Widgets)
    : IRequest;

/// <summary>A widget-to-be from an import document — everything but identity and ordinal.</summary>
[ExcludeFromCodeCoverage]
public sealed record HomePageWidgetSpec(string WidgetType, string? Title, string SizePreset,
    string ConfigJson, int ConfigVersion);
