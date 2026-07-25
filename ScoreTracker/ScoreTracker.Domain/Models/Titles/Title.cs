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
    ///     The progress baseline below which "how close" is meaningless — 0 for a
    ///     standalone title, the rung below it for a title on a ladder (see
    ///     <see cref="TitleHelpers.LinkLadder{TTitle,TKey}" />), and a decent pass for
    ///     skill titles so a barely-passed chart doesn't read as ~98% of the way to the SSS.
    /// </summary>
    public int CompletionFloor { get; private set; }

    /// <summary>
    ///     Sets where this title's progress starts measuring from. Ladder rungs get the
    ///     requirement of the rung below them; skill titles set their own floor.
    /// </summary>
    internal void FloorAt(int floor)
    {
        CompletionFloor = floor;
    }
}