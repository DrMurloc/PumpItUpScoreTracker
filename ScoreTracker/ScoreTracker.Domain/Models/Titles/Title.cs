using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Models.Titles;

public abstract class Title
{
    protected Title(Name name, string description, Name category, int completionRequired)
    {
        Name = name;
        Description = description;
        Category = category;
        CompletionRequired = completionRequired;
    }

    protected Title(Name name, string description, Name category) : this(name, description, category, 0)
    {
    }

    public Name Name { get; }
    public Name Category { get; }
    public string Description { get; }
    public int CompletionRequired { get; }
}