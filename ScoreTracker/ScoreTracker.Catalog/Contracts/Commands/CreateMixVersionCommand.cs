using MediatR;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Catalog.Contracts.Commands;

/// <summary>
///     Adds a patch to a mix at the end of its release order — the admin BulkAddCharts picker's
///     "new version" entry (docs/design/chart-versions.md §6). Returns the row's id; a name the mix
///     already has returns the existing row rather than a second one, so a re-run of a batch is
///     harmless.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record CreateMixVersionCommand(MixEnum Mix, string Name, DateOnly? ReleaseDate) : IRequest<Guid>;
