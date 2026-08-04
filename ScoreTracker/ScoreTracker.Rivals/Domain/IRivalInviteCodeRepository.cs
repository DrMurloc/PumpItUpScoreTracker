namespace ScoreTracker.Rivals.Domain;

/// <summary>
///     The one code a private account hands out so somebody can add them (docs/design/rivals.md
///     D23–D25). Vertical-internal.
/// </summary>
internal interface IRivalInviteCodeRepository
{
    /// <summary>The user's current code, or null when they have never had one minted.</summary>
    Task<string?> GetCodeFor(Guid userId, CancellationToken cancellationToken);

    /// <summary>Whose code this is, or null when it matches nobody — including after a recycle.</summary>
    Task<Guid?> GetUserForCode(string code, CancellationToken cancellationToken);

    /// <summary>
    ///     Writes the user's code, replacing any previous one. Returns false when the code collided
    ///     with somebody else's, so the caller can draw again rather than silently reusing it.
    /// </summary>
    Task<bool> TrySetCode(Guid userId, string code, DateTimeOffset at, CancellationToken cancellationToken);
}
