using MediatR;

namespace ScoreTracker.Identity.Contracts.Queries;

/// <summary>
///     The current user's merges still inside the grace window (undoable).
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetActiveAccountMergesQuery : IQuery<IEnumerable<AccountMergeRecord>>
{
}
