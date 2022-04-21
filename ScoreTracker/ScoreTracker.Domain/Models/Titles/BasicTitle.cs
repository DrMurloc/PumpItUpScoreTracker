using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Models.Titles;

public sealed class BasicTitle : Title
{
    public BasicTitle(Name name, string description) : base(name, description, "Misc.")
    {
    }

    public BasicTitle(Name name, string description, Name category) : base(name, description, category)
    {
    }

    public override bool DoesAttemptApply(BestChartAttempt attempt)
    {
        return false;
    }
}