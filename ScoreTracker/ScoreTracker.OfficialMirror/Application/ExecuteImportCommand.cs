using MediatR;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.OfficialMirror.Application;

// In-process request the background consumer sends to run the shared import body on the saga.
// Internal — not a cross-vertical contract.
[ExcludeFromCodeCoverage]
// Returns how many records the run changed, so the consumer can stamp it on the ImportResult
// without waiting on the Ledger's batch-drain counter.
internal sealed record ExecuteImportCommand(Guid UserId, MixEnum Mix, string Sid, string CardId,
    string ExpectedGameTag, bool IncludeBroken, Guid? SessionId = null) : IRequest<int>;
