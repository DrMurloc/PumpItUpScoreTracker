using MediatR;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.OfficialMirror.Application;

/// <summary>
///     What a check run did, handed back to the consumer that has to record it.
///     <para>
///         <paramref name="SessionId" /> is null when the run never opened one — a deep scan
///         refused the site-wide slot. Asking for it back is what keeps the session minted AFTER
///         that gate: minting it in the consumer instead would leave an empty session row in the
///         player's list every time a deep scan lost the race.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed record ImportCheckRun(Guid? SessionId, int Saved);

// In-process request the background consumer sends to run the check body on the saga.
// Internal — not a cross-vertical contract.
[ExcludeFromCodeCoverage]
internal sealed record ExecuteImportCheckCommand(Guid UserId, MixEnum Mix, string Sid, string CardId,
    string ExpectedGameTag, bool DeepScan, bool IncludeBroken) : IRequest<ImportCheckRun>;
