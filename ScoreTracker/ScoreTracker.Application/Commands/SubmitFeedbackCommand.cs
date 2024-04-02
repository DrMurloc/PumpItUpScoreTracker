using MediatR;
using ScoreTracker.Domain.Records;

namespace ScoreTracker.Application.Commands
{
    public sealed record SubmitFeedbackCommand(SuggestionFeedbackRecord Feedback) : IRequest
    {
    }
}
