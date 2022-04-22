using MediatR;
using ScoreTracker.Domain.Models.Upload;

namespace ScoreTracker.Application.Commands;

public sealed record UploadAttemptsCommand
    (IEnumerable<BestChartAttemptUploadAttempt> Attempts) : IRequest<IEnumerable<BestChartAttemptUploadFailure>>
{
}