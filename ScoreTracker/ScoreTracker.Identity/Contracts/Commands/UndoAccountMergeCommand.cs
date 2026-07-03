using MediatR;

namespace ScoreTracker.Identity.Contracts.Commands;

/// <summary>
///     Reverses an account merge within the grace window: moved sign-in methods return to the
///     retired account and its visibility is restored. Only the survivor (who holds every
///     login) can undo.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record UndoAccountMergeCommand(Guid MergeRequestId) : IRequest
{
}
