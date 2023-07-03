using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Models.Titles.XX;

public sealed class XXCustomTitle : XXTitle
{
    private readonly Func<BestXXChartAttempt, bool> _rule;

    public XXCustomTitle(Name name, string description, int completionRequired, Name category,
        Func<BestXXChartAttempt, bool> rule) : base(
        name, description, category, completionRequired)
    {
        _rule = rule;
    }

    public XXCustomTitle(Name title, string description, Name category, Func<BestXXChartAttempt, bool> rule) : this(
        title,
        description, 0, category,
        rule)
    {
    }

    public XXCustomTitle(Name title, string description, Func<BestXXChartAttempt, bool> rule) : this(title, description,
        0,
        "Misc.",
        rule)
    {
    }

    public override bool DoesAttemptApply(BestXXChartAttempt attempt)
    {
        return _rule(attempt);
    }
}