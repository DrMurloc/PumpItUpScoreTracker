using MediatR;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.EventCompetition.Contracts.Commands
{
    /// <summary>
    ///     A score the player entered. PhotoUrl is required; the handler stamps the timestamp.
    ///     Judgements are supplied instead of a score when the play came off an XX cabinet.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed record SubmitQualifierScoreCommand(
        Guid TournamentId,
        Name UserName,
        Guid ChartId,
        Uri PhotoUrl,
        PhoenixScore? Score = null,
        XxJudgements? Judgements = null) : IRequest
    {
    }
}
