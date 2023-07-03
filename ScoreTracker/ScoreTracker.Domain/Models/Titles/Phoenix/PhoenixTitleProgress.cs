namespace ScoreTracker.Domain.Models.Titles.Phoenix;

public sealed class PhoenixTitleProgress : TitleProgress
{
    public PhoenixTitleProgress(PhoenixTitle title) : base(title)
    {
        PhoenixTitle = title;
    }

    public PhoenixTitle PhoenixTitle { get; }

    public override int CompletionCount { get; protected set; }

    public void ApplyAttempt(RecordedPhoenixScore attempt)
    {
        if (PhoenixTitle.DoesAttemptApply(attempt)) CompletionCount++;
    }
}