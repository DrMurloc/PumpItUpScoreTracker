using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.OfficialMirror.Domain;

/// <summary>
///     Player identity on the mirror: the import-confirmed UserId link and the
///     rename-proposal lifecycle. A merge re-points the old player's history onto the
///     new player id and deletes the old dimension row — the proposal row keeps both
///     usernames as the audit trail.
/// </summary>
internal interface IOfficialPlayerIdentityRepository
{
    /// <summary>
    ///     Import-confirmed link: upserts the (mix, username) player and points it at the
    ///     user, overwriting any previous link — the most recent import wins.
    /// </summary>
    Task LinkPlayer(MixEnum mix, string username, Guid userId, DateTimeOffset seenAt, CancellationToken ct);

    /// <summary>Re-points every mirror player linked to one account onto another (account merges).</summary>
    Task RelinkUser(Guid fromUserId, Guid toUserId, CancellationToken ct);

    Task WriteProposals(MixEnum mix, IReadOnlyCollection<RenameProposal> proposals, CancellationToken ct);
    Task<IReadOnlyList<RenameProposal>> GetProposals(MixEnum mix, string status, CancellationToken ct);
    Task<RenameProposal?> GetProposal(int id, CancellationToken ct);
    Task SetProposalStatus(int id, string status, CancellationToken ct);
    Task MergePlayers(int oldPlayerId, int newPlayerId, CancellationToken ct);
}
