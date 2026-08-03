using MediatR;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.OfficialMirror.Application;

// In-process request the completeness check sends to re-read the levels it found short and save
// whatever beats what we hold. An empty bucket list means the whole account — the deep scan.
// Internal — not a cross-vertical contract. Returns how many records it raised.
[ExcludeFromCodeCoverage]
internal sealed record RepairScoresCommand(Guid UserId, MixEnum Mix, string Sid, string CardId,
    string ExpectedGameTag, IReadOnlyCollection<string> Buckets, bool IncludeBroken) : IRequest<int>;
