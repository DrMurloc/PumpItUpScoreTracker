using ScoreTracker.Domain.Models.Titles;
using ScoreTracker.Domain.Models.Titles.Phoenix;

namespace ScoreTracker.Web.Dtos;

public sealed class TitleProgressDto
{
    public string TitleName { get; set; } = string.Empty;
    public string TitleDescription { get; set; } = string.Empty;
    public string TitleCategory { get; set; } = string.Empty;
    public bool IsTrackable => RequiredCount > 0;
    public int CompletionCount { get; set; }
    public int RequiredCount { get; set; }
    public string AdditionalNote { get; set; } = string.Empty;
    public int? DifficultyLevel { get; set; }

    public static TitleProgressDto From(TitleProgress progress)
    {
        return new TitleProgressDto
        {
            CompletionCount = progress.CompletionCount,
            RequiredCount = progress.Title.CompletionRequired,
            TitleCategory = progress.Title.Category,
            TitleDescription = progress.Title.Description,
            TitleName = progress.Title.Name,
            AdditionalNote = progress.AdditionalNote,
            DifficultyLevel = progress.Title is PhoenixDifficultyTitle pdt ? pdt.Level : null
        };
    }
}
