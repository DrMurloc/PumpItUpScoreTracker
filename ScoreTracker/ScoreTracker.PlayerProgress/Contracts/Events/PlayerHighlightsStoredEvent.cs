using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.PlayerProgress.Contracts.Events;

/// <summary>
///     A score batch's significant wins have been classified and stored. Published only when the
///     classification actually produced wins, so an audience index never records a silent event.
///     <para>
///         This exists so an audience can index the win without owning the payload: once the
///         capture lives in PlayerProgress it cannot write Communities' table, and Communities
///         needs no payload of its own to record that an event is visible to a community
///         (docs/design/rivals.md §4.2). Carries no wins for the same reason — a consumer that
///         wants them asks for them.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record PlayerHighlightsStoredEvent(
    Guid EventId,
    Guid UserId,
    MixEnum Mix,
    DateTimeOffset OccurredAt);
