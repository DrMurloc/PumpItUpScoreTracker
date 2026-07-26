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

    /// <summary>
    ///     The rail this title is drawn on — a folder tier, a skill track, a plate family,
    ///     a mix's boss pair — or null for a one-off that belongs to no progression.
    ///     <para>
    ///         Deliberately NOT the same grouping as <see cref="TitleHelpers.LinkLadder{TTitle,TKey}" />,
    ///         which groups by what SCORING shares. Advanced is the worked example: three
    ///         scoring ladders (the 20s, the 21s, the 22s, each floored on its own level) but
    ///         one rail the player reads as Lv.1 through Lv.10.
    ///     </para>
    /// </summary>
    public Name? Ladder { get; private set; }

    /// <summary>
    ///     1-based position on <see cref="Ladder" />, and 0 when there is no rail. Requirement
    ///     order cannot stand in for it: Advanced Lv.3 (39,000) outranks Lv.4 (15,000), Expert
    ///     Lv.1 and Lv.6 are both 40,000, and every skill and boss-breaker title requires 1.
    /// </summary>
    public int Rung { get; private set; }

    /// <summary>Places this title on a display rail. See <see cref="Ladder" />.</summary>
    internal void OnRail(Name ladder, int rung)
    {
        Ladder = ladder;
        Rung = rung;
    }
}