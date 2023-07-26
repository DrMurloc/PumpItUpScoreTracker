namespace ScoreTracker.Domain.Models.Titles;

public abstract class TitleProgress
{
    protected TitleProgress(Title title)
    {
        Title = title;
    }

    public Title Title { get; }

    public abstract int CompletionCount { get; protected set; }
    public virtual string AdditionalNote => string.Empty;
}