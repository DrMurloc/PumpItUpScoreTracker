namespace ScoreTracker.Rivals.Contracts;

/// <summary>
///     Somebody who rivals you. <see cref="IsMutual" /> is what turns the reverse list from a
///     stranger-count into something readable — most of these will be people you already chose.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record RivalOfMeRecord(
    Guid EdgeId,
    Guid UserId,
    string PlayerName,
    Uri Avatar,
    bool IsPublic,
    bool SharesCommunity,
    bool IsMutual,
    DateTimeOffset AddedAt);

[ExcludeFromCodeCoverage]
public sealed record BlockedPlayerRecord(Guid UserId, string PlayerName, Uri Avatar, DateTimeOffset BlockedAt);

/// <summary>
///     What the invite landing page shows before you commit. Deliberately just the person: a code
///     is a handshake, not a profile.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record RivalInvitePreviewRecord(Guid UserId, string PlayerName, Uri Avatar, bool AlreadyRival);
