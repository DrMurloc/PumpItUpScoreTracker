using MediatR;
using ScoreTracker.Domain.Records;

namespace ScoreTracker.PlayerProgress.Contracts.Commands
{
    [ExcludeFromCodeCoverage]
    public sealed record SubmitFeedbackCommand(SuggestionFeedbackRecord Feedback) : IRequest
    {
    }
}
