using MediatR;

namespace ScoreTracker.OfficialMirror.Contracts.Commands;

/// <summary>
///     Acceptance of a detected rename: the old tag's board history re-points onto the new
///     tag's player and the old dimension row is deleted. The finding row keeps both
///     usernames as the audit trail, which is the only record that survives the merge.
/// </summary>
/// <param name="Unattended">
///     True when the sweep decided this on its own. It changes nothing about what the merge
///     does — only the status left behind, so the desk can always answer which of these a
///     human actually looked at. The merge cannot be undone; that question gets asked.
/// </param>
[ExcludeFromCodeCoverage]
public sealed record AcceptRenameProposalCommand(int ProposalId, bool Unattended = false) : IRequest;
