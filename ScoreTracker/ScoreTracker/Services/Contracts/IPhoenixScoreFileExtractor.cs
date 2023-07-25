using Microsoft.AspNetCore.Components.Forms;
using ScoreTracker.Domain.Models;
using ScoreTracker.Web.Dtos;

namespace ScoreTracker.Web.Services.Contracts;

public interface IPhoenixScoreFileExtractor
{
    Task<(IEnumerable<RecordedPhoenixScore> Scores, IEnumerable<SpreadsheetScoreErrorDto> Errors)> GetScores(
        IBrowserFile file,
        CancellationToken cancellationToken = default);
}