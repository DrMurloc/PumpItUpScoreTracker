using MediatR;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.OfficialMirror.Application;

// In-process request the background consumer sends to run the check body on the saga.
// Internal — not a cross-vertical contract.
[ExcludeFromCodeCoverage]
// Returns the score session the run opened, or null when it never opened one — a deep scan
// refused the site-wide slot. The consumer needs it to point the ImportResult at the session,
// and asking for it back is what keeps the session minted AFTER that gate: minting it in the
// consumer instead would leave an empty session row in the player's list every time a deep
// scan lost the race.
internal sealed record ExecuteImportCheckCommand(Guid UserId, MixEnum Mix, string Sid, string CardId,
    string ExpectedGameTag, bool DeepScan) : IRequest<Guid?>;
