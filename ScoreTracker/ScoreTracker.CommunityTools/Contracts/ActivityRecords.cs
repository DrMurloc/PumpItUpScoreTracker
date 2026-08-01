namespace ScoreTracker.CommunityTools.Contracts;

/// <summary>
///     The console's closed event vocabulary.
///     <para>
///         A maker-facing surface is not an admin page, so nothing here is a stack trace or a
///         framework string. Each kind renders as a curated phrase plus the remote's own status code
///         — which is why <c>DiagnosticExposureTests</c> needs no exemption for this feature.
///     </para>
/// </summary>
public enum ToolActivityKind
{
    DeliverySucceeded,
    DeliveryTimedOut,
    DeliveryRejected,
    DeliveryUnreachable,

    /// <summary>Hourly roll-up. One bad loop must not be able to flood the table or the page.</summary>
    RateLimited,

    /// <summary>Hourly roll-up of successful calls — a maker's proof the key is carrying traffic.</summary>
    KeyUsed,

    KeyExpired,
    PlayerConnected,
    PlayerDisconnected
}

/// <summary>One row of the activity log.</summary>
[ExcludeFromCodeCoverage]
public sealed record ToolActivityRecord(
    Guid Id,
    ToolActivityKind Kind,
    DateTimeOffset OccurredAt,
    DateTimeOffset? WindowStart,
    int Count,
    string? Detail,
    int? RemoteStatusCode,
    string? DeliveryId,
    Guid? DeliveryRowId,
    bool CanReplay);

/// <summary>The tiles above the log.</summary>
[ExcludeFromCodeCoverage]
public sealed record ToolActivitySummary(
    int Deliveries,
    int Failures,
    int RateLimited,
    int ConnectedPlayers);

/// <summary>
///     The exact bytes we signed for a tool's most recent delivery, next to the signature we
///     computed. HMAC mismatches are almost always a re-serialized body, and this is the difference
///     between two days of guessing and twenty minutes.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record SignatureEcho(string DeliveryId, string SignedPayload, string Signature,
    DateTimeOffset SignedAt);

/// <summary>One delivery as the pull feed reports it. Body is null once it has aged out.</summary>
[ExcludeFromCodeCoverage]
public sealed record DeliveryFeedRecord(string DeliveryId, DateTimeOffset SentAt, string Mode,
    Guid UserId, string Mix, string? Body);
