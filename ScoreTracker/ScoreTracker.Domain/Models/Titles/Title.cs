using ScoreTracker.SharedKernel.ValueTypes;

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

    /// <summary>
    ///     The progress baseline below which "how close" is meaningless — 0 for
    ///     rating-accumulation titles, but skill titles floor at a decent pass so a
    ///     barely-passed chart doesn't read as ~98% of the way to the SSS.
    /// </summary>
    public virtual int CompletionFloor => 0;
}