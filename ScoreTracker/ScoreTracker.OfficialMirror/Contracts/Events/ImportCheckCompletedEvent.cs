using MediatR;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.OfficialMirror.Contracts.Events;

/// <summary>
///     A completeness check finished, carrying its whole verdict. Nothing is stored, so this event
///     IS the result — a page that has navigated away simply never receives it, which is what the
///     panel's "stay on this page" line is telling the player.
///     <c>Repaired</c> is how many records the run raised, which the panel reports back as
///     "Added N scores".
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record ImportCheckCompletedEvent(Guid UserId, MixEnum Mix, ImportCheckReport Report, int Repaired)
    : INotification;
