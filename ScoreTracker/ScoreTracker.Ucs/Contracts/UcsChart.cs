using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Ucs.Contracts;

[ExcludeFromCodeCoverage]
public sealed record UcsChart(int PiuGameId, Chart Chart, Name Uploader, Name Artist, string Description,
    int SubmissionCount)
{
}
