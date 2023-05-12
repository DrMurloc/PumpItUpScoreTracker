using Microsoft.AspNetCore.Components.Forms;
using ScoreTracker.Domain.Records;

namespace ScoreTracker.Web.Services.Contracts;

public interface IScoreImageExtractor
{
    Task<ScoreScreen> GetScore(IBrowserFile file, CancellationToken cancellationToken);
}