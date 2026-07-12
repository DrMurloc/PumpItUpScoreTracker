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
///     THE reorder operation (D6): drag and the mobile arrows both dispatch this.
///     NewOrdinal is the insertion index, clamped.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record MoveHomePageWidgetCommand(Guid WidgetId, int NewOrdinal) : IRequest;

[ExcludeFromCodeCoverage]
public sealed record ResizeHomePageWidgetCommand(Guid WidgetId, string SizePreset) : IRequest;

/// <summary>Null title falls back to the widget type's localized display name.</summary>
[ExcludeFromCodeCoverage]
public sealed record RenameHomePageWidgetCommand(Guid WidgetId, string? Title) : IRequest;

[ExcludeFromCodeCoverage]
public sealed record UpdateHomePageWidgetConfigCommand(Guid WidgetId, string ConfigJson, int ConfigVersion)
    : IRequest;
