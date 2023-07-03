using ScoreTracker.Domain.Models.Titles;

namespace ScoreTracker.Web.Dtos;

public sealed class TitleProgressDto
{
    public string TitleName { get; set; }
    public string TitleDescription { get; set; }
    public string TitleCategory { get; set; }
    public bool IsTrackable => RequiredCount > 0;
    public int CompletionCount { get; set; }
    public int RequiredCount { get; set; }

    public static TitleProgressDto From(TitleProgress progress)
    {
        return new TitleProgressDto
        {
            CompletionCount = progress.CompletionCount,
            RequiredCount = progress.Title.CompletionRequired,
            TitleCategory = progress.Title.Category,
            TitleDescription = progress.Title.Description,
            TitleName = progress.Title.Name
        };
    }
}