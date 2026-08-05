namespace ScoreTracker.OfficialMirror.Contracts;

/// <summary>
///     A resolved board player. <see cref="LinkedUserId" /> is what decides whether the tag stands
///     for a person we know: set means an import confirmed the account, null means a ghost.
///     <see cref="Tag" /> is the NORMALIZED spelling actually stored — the one a caller should keep.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record OfficialPlayerResolution(
    string Tag,
    Guid? LinkedUserId,
    Uri? Avatar,
    bool IsOnCurrentBoards);
