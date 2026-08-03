using ScoreTracker.Catalog.Contracts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Messaging;

namespace ScoreTracker.Catalog.Contracts.Queries;

/// <summary>
///     Every song carrying at least one chart in the mix, with its full metadata.
///     <c>GetSongNamesQuery</c> answers a narrower question — names for an autocomplete — and cannot
///     serve a consumer that wants artist, duration or BPM.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetSongsQuery(MixEnum Mix) : IQuery<IReadOnlyList<SongRecord>>;
