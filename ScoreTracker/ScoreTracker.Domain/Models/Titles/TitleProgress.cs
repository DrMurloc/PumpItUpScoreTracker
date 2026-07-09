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
        _forcedComplete || (Title.CompletionRequired > 0 && CompletionCount >= Title.CompletionRequired);

    public void Complete()
    {
        _forcedComplete = true;
    }

    /// <summary>
    ///     Progress toward completion as a 0–1 fraction, rebased on the title's
    ///     <see cref="Titles.Title.CompletionFloor" /> so skill titles measure the climb
    ///     from a decent pass to the target rather than from zero.
    /// </summary>
    public double PercentComplete
    {
        get
        {
            var span = Title.CompletionRequired - Title.CompletionFloor;
            return span <= 0 ? 0 : Math.Clamp((CompletionCount - Title.CompletionFloor) / (double)span, 0, 1);
        }
    }

    public virtual string AdditionalNote => string.Empty;
}