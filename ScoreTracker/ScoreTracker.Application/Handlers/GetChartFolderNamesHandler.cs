using MediatR;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Application.Handlers;

public sealed class GetChartFolderNamesHandler : IRequestHandler<GetChartFolderNamesQuery, IEnumerable<Name>>
{
    private static IEnumerable<Name>? _names;

    public Task<IEnumerable<Name>> Handle(GetChartFolderNamesQuery request, CancellationToken cancellationToken)
    {
        return Task.FromResult(GetOrBuildFolders());
    }

    private static IEnumerable<Name> GetOrBuildFolders()
    {
        if (_names != null) return _names;

        var names = new List<Name>
        {
            "Short Cut",
            "Remix",
            "Full Song"
        };
        foreach (var level in DifficultyLevel.AllLevels)
        {
            names.Add(DifficultyLevel.ToShorthand(ChartType.Single, level));
            names.Add(DifficultyLevel.ToShorthand(ChartType.Double, level));
        }

        _names = names;
        return names;
    }
}