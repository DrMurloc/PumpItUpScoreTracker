using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Models.Titles.Phoenix;

public sealed class PhoenixBasicTitle : PhoenixTitle
{
    public PhoenixBasicTitle(Name name, string description) : base(name, description, "Misc.")
    {
    }

    public PhoenixBasicTitle(Name name, string description, Name category) : base(name, description, category)
    {
    }

    public override bool DoesAttemptApply(RecordedPhoenixScore attempt)
    {
        return false;
    }
}