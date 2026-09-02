using MediatR;

namespace ScoreTracker.Application.Commands;

/// <summary>
///     Warms the share-card renderer's image cache for one batch of art URLs — the download
///     dialog's progress loop sends these before the render, so the bar counts real images
///     (design doc §8). Idempotent, and a command rather than a query: it exists to change
///     the cache, not to read anything.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record PrefetchShareCardArtCommand(IReadOnlyList<string> Urls) : IRequest;
