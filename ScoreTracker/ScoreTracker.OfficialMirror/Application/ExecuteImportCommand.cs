using MediatR;
using ScoreTracker.OfficialMirror.Contracts;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.OfficialMirror.Application;

// In-process request the background consumer sends to run the import body on the saga, at whichever
// depth the player pressed, into the session the consumer opened. Returns how many records the run
// wrote, so the consumer can stamp it on the ImportResult row. Internal — not a cross-vertical contract.
[ExcludeFromCodeCoverage]
internal sealed record ExecuteImportCommand(Guid UserId, MixEnum Mix, string Sid, string CardId,
    string ExpectedGameTag, bool IncludeBroken, Guid SessionId, ImportKind Kind) : IRequest<int>;
