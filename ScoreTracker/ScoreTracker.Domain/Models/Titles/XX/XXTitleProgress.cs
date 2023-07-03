namespace ScoreTracker.Domain.Models.Titles.XX;

public sealed class XXTitleProgress : TitleProgress
{
    public XXTitleProgress(XXTitle title) : base(title)
    {
        XXTitle = title;
    }

    public XXTitle XXTitle { get; }

    public override int CompletionCount { get; protected set; }

    public void ApplyAttempt(BestXXChartAttempt attempt)
    {
        if (XXTitle.DoesAttemptApply(attempt)) CompletionCount++;
    }
}