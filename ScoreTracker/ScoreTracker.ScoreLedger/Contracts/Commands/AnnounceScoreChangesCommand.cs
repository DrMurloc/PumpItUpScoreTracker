using MediatR;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ScoreLedger.Contracts.Commands;

/// <summary>
///     An official import announces what it saved, once, the moment its last score is in
///     (docs/design/import-restart-recovery.md §0). <paramref name="Saves" /> are the results its saves
///     handed back; <paramref name="TitlesFound" /> the titles piugame showed on the account.
///     <para>
///         The Ledger folds the saves, takes any typed-entry batch the player has open into the same
///         announcement, and publishes one <c>PlayerScoresUpdatedEvent</c> carrying the titles. When
///         nothing changed there is no score event: the session is marked processed and the titles go
///         out on <c>TitlesDetectedEvent</c>, which is how a run with nothing new still earns badges.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record AnnounceScoreChangesCommand(
    Guid UserId,
    MixEnum Mix,
    Guid SessionId,
    IReadOnlyList<ScoreSaveResult> Saves,
    IReadOnlyList<string>? TitlesFound = null) : IRequest;
