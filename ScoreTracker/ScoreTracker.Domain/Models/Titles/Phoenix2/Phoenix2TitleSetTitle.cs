using ScoreTracker.Domain.Models.Titles.Phoenix;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Domain.Models.Titles.Phoenix2;

/// <summary>
///     A Phoenix 2 meta-title earned by completing other titles: each skill ladder's EXPERT
///     ("earn 10 titles") and SPECIALIST ("earn all skill titles"). Progress is the count of
///     completed member titles, applied by <see cref="Phoenix2TitleList.BuildProgress" />
///     after completions resolve — membership is by category over the chart-grade titles.
/// </summary>
public sealed class Phoenix2TitleSetTitle : PhoenixTitle
{
    private readonly HashSet<Name> _memberCategories;

    public Phoenix2TitleSetTitle(Name name, string description, Name category,
        IEnumerable<Name> memberCategories, int required)
        : base(name, description, category, required)
    {
        _memberCategories = memberCategories.ToHashSet();
    }

    public override bool PopulatesFromDatabase => false;

    public bool CountsMember(PhoenixTitle title)
    {
        return title is Phoenix2ChartGradeTitle && _memberCategories.Contains(title.Category);
    }
}
