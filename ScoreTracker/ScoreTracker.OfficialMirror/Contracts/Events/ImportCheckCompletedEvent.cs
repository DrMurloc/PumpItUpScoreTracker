using MediatR;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.OfficialMirror.Contracts.Events;

/// <summary>
///     A completeness check finished. Anything it found was already saved as a normal import, so
///     this carries a count rather than a verdict — the scores themselves are the result, and they
///     are on the player's sessions page whether or not anyone was watching this event arrive.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record ImportCheckCompletedEvent(Guid UserId, MixEnum Mix, int Added, int Checked) : INotification;
