using MediatR;
using ScoreTracker.Catalog.Contracts;
using ScoreTracker.Catalog.Contracts.Commands;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.Catalog.Domain;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Catalog.Application;

/// <summary>
///     The mix's channels for the API, the drawers and the randomizer panel
///     (docs/design/song-channels.md §4). Counted off the per-mix chart dictionary rather than
///     read from the table, the versions handler's shape: the dictionary already carries every
///     song's channel on the mix and is already cached, and a channel no song of the mix sits
///     in is not one the mix offers.
/// </summary>
internal sealed class GetMixChannelsHandler : IRequestHandler<GetMixChannelsQuery, IReadOnlyList<MixChannelRecord>>
{
    private readonly IChartRepository _charts;

    public GetMixChannelsHandler(IChartRepository charts)
    {
        _charts = charts;
    }

    public async Task<IReadOnlyList<MixChannelRecord>> Handle(GetMixChannelsQuery request,
        CancellationToken cancellationToken)
    {
        var charts = await _charts.GetCharts(request.Mix, cancellationToken: cancellationToken);
        return charts
            .Where(c => c.Song.Channel != null)
            .GroupBy(c => c.Song.Channel!.Value)
            .OrderBy(g => g.Key)
            .Select(g => new MixChannelRecord(request.Mix, g.Key,
                g.Select(c => c.Song.Name).Distinct(new NameComparer()).Count(), g.Count()))
            .ToArray();
    }

    /// <summary>A song is one song however many charts of it the mix has.</summary>
    private sealed class NameComparer : IEqualityComparer<Name>
    {
        public bool Equals(Name x, Name y)
        {
            return x.ToString().Equals(y.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        public int GetHashCode(Name obj)
        {
            return StringComparer.OrdinalIgnoreCase.GetHashCode(obj.ToString());
        }
    }
}

internal sealed class SetSongChannelHandler : IRequestHandler<SetSongChannelCommand>
{
    private readonly ISongMixRepository _songMixes;

    public SetSongChannelHandler(ISongMixRepository songMixes)
    {
        _songMixes = songMixes;
    }

    public Task Handle(SetSongChannelCommand request, CancellationToken cancellationToken)
    {
        return _songMixes.SetChannel(request.Mix, request.SongId, request.Channel, cancellationToken);
    }
}
