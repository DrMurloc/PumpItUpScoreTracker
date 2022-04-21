using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Models.Titles;

public sealed class CustomTitle : Title
{
    private readonly Func<BestChartAttempt, bool> _rule;

    public CustomTitle(Name name, string description, int completionRequired, Name category,
        Func<BestChartAttempt, bool> rule) : base(
        name, description, category, completionRequired)
    {
        _rule = rule;
    }

    public CustomTitle(Name title, string description, Name category, Func<BestChartAttempt, bool> rule) : this(title,
        description, 0, category,
        rule)
    {
    }

    public CustomTitle(Name title, string description, Func<BestChartAttempt, bool> rule) : this(title, description, 0,
        "Misc.",
        rule)
    {
    }

    public override bool DoesAttemptApply(BestChartAttempt attempt)
    {
        return _rule(attempt);
    }
}