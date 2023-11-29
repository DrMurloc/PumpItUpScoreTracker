using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Models.Titles.Phoenix;

public abstract class PhoenixTitle : Title
{
    protected PhoenixTitle(Name name, string description, Name category, int completionRequired) : base(name,
        description, category, completionRequired)
    {
    }

    protected PhoenixTitle(Name name, string description, Name category) : this(name, description, category, 0)
    {
    }

    public virtual int CompletionProgress(Chart chart, RecordedPhoenixScore attempt)
    {
        return 0;
    }
}
