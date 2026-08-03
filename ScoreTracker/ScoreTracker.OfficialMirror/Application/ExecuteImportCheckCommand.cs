using MediatR;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.OfficialMirror.Application;

// In-process request the background consumer sends to run the check body on the saga.
// Internal — not a cross-vertical contract.
[ExcludeFromCodeCoverage]
internal sealed record ExecuteImportCheckCommand(Guid UserId, MixEnum Mix, string Sid, string CardId,
    string ExpectedGameTag, bool DeepScan, bool Repair = false) : IRequest;
