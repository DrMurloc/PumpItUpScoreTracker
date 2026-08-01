using MediatR;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ScoreLedger.Contracts.Commands;

/// <summary>
///     Opens a session up front and returns its id, for callers that know something about the
///     run the per-score path never sees — an official import knows which card it is pulling
///     from. The id is then passed as the explicit SessionId on every submission, so the
///     metadata is recorded once instead of riding thousands of score commands.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record BeginScoreSessionCommand(
    Guid UserId,
    MixEnum Mix,
    string Source,
    string? AccountTag = null,
    string? CardId = null) : IRequest<Guid>;
