using MediatR;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.OfficialMirror.Application;

// In-process request the background consumer sends to run the shared import body on the saga.
// Internal — not a cross-vertical contract.
[ExcludeFromCodeCoverage]
internal sealed record ExecuteImportCommand(Guid UserId, MixEnum Mix, string Sid, string CardId,
    string ExpectedGameTag, bool IncludeBroken, bool SyncPiuTracker) : IRequest;
