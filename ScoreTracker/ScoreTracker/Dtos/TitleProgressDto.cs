using ScoreTracker.Domain.Enums;
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
    public bool IsCompleted { get; set; }
    public int RequiredCount { get; set; }
    public string AdditionalNote { get; set; } = string.Empty;
    public int? DifficultyLevel { get; set; }
    public bool HasParagonLevel { get; set; }
    public PhoenixLetterGrade? ParagonLevel { get; set; }
    public int NextParagonProgress { get; set; }
    public int NextParagonRequirement { get; set; }

    public static TitleProgressDto From(TitleProgress progress)
    {
        var result = new TitleProgressDto
        {
            CompletionCount = (int)progress.CompletionCount,
            RequiredCount = progress.Title.CompletionRequired,
            TitleCategory = progress.Title.Category,
            TitleDescription = progress.Title.Description,
            TitleName = progress.Title.Name,
            AdditionalNote = progress.AdditionalNote,
            IsCompleted = progress.IsComplete,
            DifficultyLevel = progress.Title is PhoenixDifficultyTitle pdt ? pdt.Level : null
        };
        if (progress is PhoenixTitleProgress phoenixProgress)
        {
            result.HasParagonLevel = phoenixProgress.RequiredAaCount > 0;
            result.ParagonLevel = phoenixProgress.ParagonLevel;
            result.NextParagonProgress = phoenixProgress.NextParagonProgress;
            result.NextParagonRequirement = phoenixProgress.RequiredAaCount;
        }

        return result;
    }
}