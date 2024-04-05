namespace ScoreTracker.Domain.Models.Titles;

public abstract class TitleProgress
{
    protected TitleProgress(Title title)
    {
        Title = title;
    }

    public Title Title { get; }

    public abstract double CompletionCount { get; protected set; }
    private bool _forcedComplete;

    public bool IsComplete =>
        _forcedComplete || (Title.CompletionRequired > 0 && CompletionCount > Title.CompletionRequired);

    public void Complete()
    {
        _forcedComplete = true;
    }

    public virtual string AdditionalNote => string.Empty;
}