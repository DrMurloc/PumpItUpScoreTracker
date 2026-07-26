using MediatR;
using ScoreTracker.Catalog.Contracts;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Catalog.Application;

internal sealed class GetMixDiffHandler : IRequestHandler<GetMixDiffQuery, MixDiffRecord>
{
    private readonly IChartRepository _charts;

    public GetMixDiffHandler(IChartRepository charts)
    {
        _charts = charts;
    }

    public async Task<MixDiffRecord> Handle(GetMixDiffQuery request, CancellationToken cancellationToken)
    {
        if (request.From == request.To) return Empty(request);

        var before = (await _charts.GetCharts(request.From, cancellationToken: cancellationToken))
            .ToDictionary(c => c.Id);
        var after = (await _charts.GetCharts(request.To, cancellationToken: cancellationToken))
            .ToDictionary(c => c.Id);

        // A song is in a mix exactly when it has a chart there, so presence is derived from
        // the same two reads rather than a third round trip for song names.
        var beforeSongs = SongsByName(before.Values);
        var afterSongs = SongsByName(after.Values);

        var arrivedSongs = SongRecords(afterSongs, beforeSongs.Keys);
        var departedSongs = SongRecords(beforeSongs, afterSongs.Keys);
        var arrivedNames = arrivedSongs.Select(s => s.Song.Name).ToHashSet();
        var departedNames = departedSongs.Select(s => s.Song.Name).ToHashSet();

        // Charts of a song that arrived or left whole are already reported by song. Only the
        // charts that came and went on their own are listed individually.
        var addedCharts = after.Values
            .Where(c => !before.ContainsKey(c.Id))
            .Where(c => !arrivedNames.Contains(c.Song.Name))
            .OrderBy(c => c.Song.Name.ToString(), StringComparer.OrdinalIgnoreCase)
            .ThenBy(c => c.Type).ThenBy(c => c.Level)
            .ToArray();

        var removedCharts = before.Values
            .Where(c => !after.ContainsKey(c.Id))
            .Where(c => !departedNames.Contains(c.Song.Name))
            .OrderBy(c => c.Song.Name.ToString(), StringComparer.OrdinalIgnoreCase)
            .ThenBy(c => c.Type).ThenBy(c => c.Level)
            .ToArray();

        var rerated = after.Values
            .Where(c => before.ContainsKey(c.Id) && before[c.Id].Level != c.Level)
            .Select(c => new MixDiffMoveRecord(before[c.Id], c))
            .OrderBy(m => m.After.Song.Name.ToString(), StringComparer.OrdinalIgnoreCase)
            .ThenBy(m => m.After.Type).ThenBy(m => m.After.Level)
            .ToArray();

        return new MixDiffRecord(request.From, request.To, rerated, arrivedSongs, departedSongs,
            addedCharts, removedCharts);
    }

    private static MixDiffRecord Empty(GetMixDiffQuery request)
    {
        return new MixDiffRecord(request.From, request.To, Array.Empty<MixDiffMoveRecord>(),
            Array.Empty<MixDiffSongRecord>(), Array.Empty<MixDiffSongRecord>(),
            Array.Empty<Chart>(), Array.Empty<Chart>());
    }

    private static Dictionary<Name, List<Chart>> SongsByName(IEnumerable<Chart> charts)
    {
        var map = new Dictionary<Name, List<Chart>>();
        foreach (var chart in charts)
        {
            if (!map.TryGetValue(chart.Song.Name, out var list)) map[chart.Song.Name] = list = new List<Chart>();
            list.Add(chart);
        }

        return map;
    }

    private static MixDiffSongRecord[] SongRecords(Dictionary<Name, List<Chart>> source,
        IEnumerable<Name> presentInOther)
    {
        var other = presentInOther.ToHashSet();
        return source
            .Where(kv => !other.Contains(kv.Key))
            .OrderBy(kv => kv.Key.ToString(), StringComparer.OrdinalIgnoreCase)
            .Select(kv => new MixDiffSongRecord(kv.Value[0].Song,
                kv.Value.OrderBy(c => c.Type).ThenBy(c => c.Level).ToArray()))
            .ToArray();
    }
}
