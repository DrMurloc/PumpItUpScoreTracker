using MediatR;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.OfficialMirror.Application;

// In-process request the background consumer sends to run the check body on the saga, into the
// session the consumer already opened and pointed its run at. Returns how many records the run
// changed, across both halves. Internal — not a cross-vertical contract.
[ExcludeFromCodeCoverage]
internal sealed record ExecuteImportCheckCommand(Guid UserId, MixEnum Mix, string Sid, string CardId,
    string ExpectedGameTag, bool DeepScan, bool IncludeBroken, Guid SessionId) : IRequest<int>;
