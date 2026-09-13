using MediatR;
using ScoreTracker.Catalog.Contracts;
using ScoreTracker.Catalog.Contracts.Commands;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.Catalog.Domain;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Catalog.Application;

/// <summary>
///     The mix's patches for the API, the drawers and the admin picker (docs/design/chart-versions.md).
///     The chart count per patch is counted off the per-mix chart dictionary rather than stored,
///     because the dictionary already carries each chart's release and is already cached.
/// </summary>
internal sealed class GetMixVersionsHandler : IRequestHandler<GetMixVersionsQuery, IReadOnlyList<MixVersionRecord>>
{
    private readonly IChartRepository _charts;
    private readonly IMixVersionRepository _versions;

    public GetMixVersionsHandler(IMixVersionRepository versions, IChartRepository charts)
    {
        _versions = versions;
        _charts = charts;
    }

    public async Task<IReadOnlyList<MixVersionRecord>> Handle(GetMixVersionsQuery request,
        CancellationToken cancellationToken)
    {
        var versions = await _versions.GetVersions(request.Mix, cancellationToken);
        if (versions.Count == 0) return Array.Empty<MixVersionRecord>();

        var counts = (await _charts.GetCharts(request.Mix, cancellationToken: cancellationToken))
            .Where(c => c.Release != null)
            .GroupBy(c => c.Release!.Version, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        return versions
            .OrderBy(v => v.SortOrder)
            .Select(v => new MixVersionRecord(v.Mix, v.Name, v.ReleaseDate, v.SortOrder,
                counts.TryGetValue(v.Name, out var n) ? n : 0))
            .ToArray();
    }
}

internal sealed class CreateMixVersionHandler : IRequestHandler<CreateMixVersionCommand, Guid>
{
    private readonly IMixVersionRepository _versions;

    public CreateMixVersionHandler(IMixVersionRepository versions)
    {
        _versions = versions;
    }

    public Task<Guid> Handle(CreateMixVersionCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("A version needs a name.", nameof(request));
        return _versions.Create(request.Mix, request.Name.Trim(), request.ReleaseDate, cancellationToken);
    }
}
