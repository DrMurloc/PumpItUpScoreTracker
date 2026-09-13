using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Catalog.Domain;

/// <summary>
///     The one write onto a song's channel per mix (docs/design/song-channels.md §3). Reads never
///     come through here: the per-mix chart dictionary carries every song's channel on that mix,
///     and the channels handler counts off it.
/// </summary>
internal interface ISongMixRepository
{
    /// <summary>
    ///     Sets the song's channel on the mix — inserting the row or overwriting it — and evicts
    ///     the mix's chart dictionary so the next read sees it. A re-run with the same value is
    ///     harmless.
    /// </summary>
    Task SetChannel(MixEnum mix, Guid songId, Channel channel, CancellationToken cancellationToken = default);
}
