using MediatR;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Catalog.Contracts.Commands;

/// <summary>
///     Sets a song's channel on one mix — the folder it sits in on that mix's song select — by
///     inserting or overwriting its SongMix row (docs/design/song-channels.md §3). The bulk-add
///     batch sends it after creating a song; a re-run with the same value is harmless.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record SetSongChannelCommand(MixEnum Mix, Guid SongId, Channel Channel) : IRequest;
