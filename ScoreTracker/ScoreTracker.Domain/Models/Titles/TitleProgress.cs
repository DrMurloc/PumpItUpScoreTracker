namespace ScoreTracker.Domain.Models.Titles;

public sealed class TitleProgress
{
    public TitleProgress(Title title)
    {
        Title = title;
    }

    public Title Title { get; }

    public int CompletionCount { get; private set; }

    public void ApplyAttempt(BestChartAttempt attempt)
    {
        if (Title.DoesAttemptApply(attempt)) CompletionCount++;
    }
}