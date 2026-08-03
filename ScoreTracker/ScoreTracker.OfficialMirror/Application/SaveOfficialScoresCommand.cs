using MediatR;
using ScoreTracker.Domain.Records;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.OfficialMirror.Application;

// In-process request to put already-scraped bests through the import's save path — same raise-only
// rule, same session, same journal. Returns how many records it actually raised.
// Internal — not a cross-vertical contract.
[ExcludeFromCodeCoverage]
internal sealed record SaveOfficialScoresCommand(Guid UserId, MixEnum Mix, Guid SessionId,
    IReadOnlyList<OfficialRecordedScore> Scores) : IRequest<int>;
