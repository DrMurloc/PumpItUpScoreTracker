using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Models.Titles.XX;

public sealed class XXBasicTitle : XXTitle
{
    public XXBasicTitle(Name name, string description) : base(name, description, "Misc.")
    {
    }

    public XXBasicTitle(Name name, string description, Name category) : base(name, description, category)
    {
    }

    public override bool DoesAttemptApply(BestXXChartAttempt attempt)
    {
        return false;
    }
}