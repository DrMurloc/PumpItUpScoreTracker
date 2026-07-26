using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Domain.Models.Titles.Phoenix;

public sealed class PhoenixBasicTitle : PhoenixTitle
{
    public PhoenixBasicTitle(Name name, string description) : base(name, description, "Misc.")
    {
    }

    /// <param name="ladder">
    ///     The rail this title draws on — a plate family, a step artist, the membership
    ///     ladder. Null for a one-off, which is most of them: a basic title has no formula
    ///     behind it, so its category alone ("Plates", "Misc.") is too coarse to be a rail.
    /// </param>
    /// <param name="rung">
    ///     1-based position on that rail, declared rather than derived. A basic title has no
    ///     requirement to sort by, and these are declared strongest-first in the lists.
    /// </param>
    public PhoenixBasicTitle(Name name, string description, Name category, Name? ladder = null, int rung = 0) :
        base(name, description, category)
    {
        if (ladder != null) OnRail(ladder.Value, rung);
    }
}