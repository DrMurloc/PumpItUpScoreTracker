using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.PlayerProgress.Contracts.Events;

/// <summary>
///     An account's completed title set is no longer what it was — one was earned, or the shipped
///     list re-evaluated an old one. Published once per save that actually moved the set, on both
///     the score path and the site-detection path.
///     <para>
///         Deliberately carries no titles. A consumer that needs to know what somebody holds reads
///         it, which keeps this immune to arriving late, twice or out of order — and to the trap
///         that an opaque value type on a bus message serializes to <c>{}</c> and comes back
///         <c>default</c> with no error at all (CLAUDE.md). A Guid and an enum cannot do that.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record PlayerTitlesChangedEvent(Guid UserId, MixEnum Mix);
