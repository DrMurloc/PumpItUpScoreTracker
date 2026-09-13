using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Catalog.Contracts.Queries;

/// <summary>
///     The channels the mix offers, in the game's order, each with how many songs and charts of
///     the mix sit in it. A mix's channel set is its SongMix rows, so Phoenix lists five and
///     Phoenix 2 four; a mix with no rows answers with an empty list
///     (docs/design/song-channels.md §3, §4).
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetMixChannelsQuery(MixEnum Mix) : IQuery<IReadOnlyList<MixChannelRecord>>;
