using MediatR;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.HomePage.Contracts.Commands;

// Page-level mutations. All operate on the CURRENT user's pages (D2: private in v1);
// targeting another user's page id fails as not-found. Every mutation persists
// immediately (§2.1 — Blazor Server circuits are not to be trusted with staged edits).

/// <summary>First page a user creates becomes their default automatically.</summary>
[ExcludeFromCodeCoverage]
public sealed record CreateHomePageCommand(Name Name) : IRequest<HomePageRecord>;

[ExcludeFromCodeCoverage]
public sealed record RenameHomePageCommand(Guid PageId, Name Name) : IRequest;

/// <summary>
///     Deletes the page and its widgets. Deleting the default page promotes the first
///     remaining page (by tab order); remaining ordinals compact.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record DeleteHomePageCommand(Guid PageId) : IRequest;

/// <summary>Moves the page to a new tab position (clamped); other pages shift.</summary>
[ExcludeFromCodeCoverage]
public sealed record ReorderHomePageCommand(Guid PageId, int NewOrdinal) : IRequest;

[ExcludeFromCodeCoverage]
public sealed record SetDefaultHomePageCommand(Guid PageId) : IRequest;

/// <summary>Null clears the page-level mix context back to follow-current-mix (D13).</summary>
[ExcludeFromCodeCoverage]
public sealed record SetHomePageMixCommand(Guid PageId, MixEnum? DefaultMix) : IRequest;
