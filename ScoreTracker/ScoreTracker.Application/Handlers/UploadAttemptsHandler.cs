using MediatR;
using ScoreTracker.Application.Commands;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Exceptions;
using ScoreTracker.Domain.Models.Upload;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Application.Handlers;

public sealed class
    UploadAttemptsHandler : IRequestHandler<UploadAttemptsCommand, IEnumerable<BestChartAttemptUploadFailure>>
{
    private readonly IMediator _mediator;

    public UploadAttemptsHandler(IMediator mediator)
    {
        _mediator = mediator;
    }

    public async Task<IEnumerable<BestChartAttemptUploadFailure>> Handle(UploadAttemptsCommand request,
        CancellationToken cancellationToken)
    {
        var resultFailures = new List<BestChartAttemptUploadFailure>();
        foreach (var attempt in request.Attempts)
        {
            if (!Name.TryParse(attempt.SongName, out var name))
            {
                resultFailures.Add(new BestChartAttemptUploadFailure(attempt, "Invalid Song Name"));
                continue;
            }

            if (!DifficultyLevel.TryParseShortHand(attempt.Difficulty, out var chartType, out var level))
            {
                resultFailures.Add(new BestChartAttemptUploadFailure(attempt, "Invalid Difficulty"));
                continue;
            }

            var parsedGrade = LetterGrade.A;
            if (!string.IsNullOrWhiteSpace(attempt.LetterGrade) &&
                !Enum.TryParse(attempt.LetterGrade, true, out parsedGrade))
            {
                resultFailures.Add(new BestChartAttemptUploadFailure(attempt, "Invalid Letter Grade"));
                continue;
            }

            LetterGrade? letterGrade = string.IsNullOrWhiteSpace(attempt.LetterGrade) ? null : parsedGrade;


            try
            {
                await _mediator.Send(new UpdateBestAttemptCommand(name, chartType, level, letterGrade, false),
                    cancellationToken);
            }
            catch (ChartNotFoundException ex)
            {
                resultFailures.Add(new BestChartAttemptUploadFailure(attempt, "Chart was not found"));
            }
            catch (Exception ex)
            {
                resultFailures.Add(new BestChartAttemptUploadFailure(attempt, "An unknown error occurred"));
            }
        }

        return resultFailures;
    }
}