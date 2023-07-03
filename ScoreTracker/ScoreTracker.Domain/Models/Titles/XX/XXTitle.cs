using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Models.Titles.XX;

public abstract class XXTitle : Title
{
    protected XXTitle(Name name, string description, Name category, int completionRequired) : base(name, description,
        category)
    {
    }

    protected XXTitle(Name title, string description, Name category) : this(title, description, category, 0)
    {
    }

    public abstract bool DoesAttemptApply(BestXXChartAttempt attempt);
}