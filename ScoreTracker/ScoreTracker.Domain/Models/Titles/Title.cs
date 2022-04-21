using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Models.Titles;

public abstract class Title
{
    protected Title(Name name, string description, Name category, int completionRequired)
    {
        Name = name;
        Description = description;
        CompletionRequired = completionRequired;
        Cateogry = category;
    }

    protected Title(Name title, string description, Name category) : this(title, description, category, 0)
    {
    }

    public Name Name { get; }
    public Name Cateogry { get; }
    public string Description { get; }
    public int CompletionRequired { get; }

    public abstract bool DoesAttemptApply(BestChartAttempt attempt);
}