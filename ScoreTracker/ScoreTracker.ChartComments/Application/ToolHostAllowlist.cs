using MediatR;
using Microsoft.Extensions.Caching.Memory;
using ScoreTracker.ChartComments.Domain;
using ScoreTracker.CommunityTools.Contracts.Queries;

namespace ScoreTracker.ChartComments.Application;

/// <summary>
///     The public-tool link allowlist, shared by every saga that parses a body. Short cache on
///     purpose: the allowlist is data — a tool approved this afternoon should be trusted by this
///     evening — but it is read on every comment render, so it cannot be a query each time.
/// </summary>
internal static class ToolHostAllowlist
{
    private const string CacheKey = $"{nameof(ToolHostAllowlist)}__TrustedToolHosts";
    private static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(10);

    public static async Task<IReadOnlyList<string>> Get(IMemoryCache cache, IMediator mediator,
        CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(CacheKey, out IReadOnlyList<string>? cached) && cached != null)
            return cached;

        var hosts = (await mediator.Send(new GetPublicToolsQuery(), cancellationToken))
            .Select(tool => tool.Url)
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .Select(url => LinkTrust.TryParse(url!)?.Host)
            .Where(host => host != null)
            .Select(host => host!)
            .Distinct()
            .ToArray();

        cache.Set(CacheKey, (IReadOnlyList<string>)hosts, CacheFor);

        return hosts;
    }
}
