using MediatR;

namespace ScoreTracker.OfficialMirror.Contracts.Commands;

/// <summary>
///     Records that the player has been shown the interrupted-import notice for this run. Sent as
///     the dialog opens, not as it closes: a dismissal the player never made — navigating away,
///     closing the tab — must not bring it back on the next page load.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record AcknowledgeImportInterruptionCommand(Guid ImportResultId) : IRequest;
