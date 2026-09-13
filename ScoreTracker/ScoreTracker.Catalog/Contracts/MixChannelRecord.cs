using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Catalog.Contracts;

/// <summary>
///     One channel of a mix as a consumer sees it (docs/design/song-channels.md §4):
///     <see cref="SongCount" /> is how many of the mix's songs sit in it, <see cref="ChartCount" />
///     how many charts. The list a mix answers with is in the game's order, the enum's.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record MixChannelRecord(MixEnum Mix, Channel Channel, int SongCount, int ChartCount);
