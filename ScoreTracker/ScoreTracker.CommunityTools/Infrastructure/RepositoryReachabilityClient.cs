using System.Net;
using System.Net.Sockets;
using System.Security.Authentication;
using ScoreTracker.CommunityTools.Contracts;
using ScoreTracker.CommunityTools.Domain;

namespace ScoreTracker.CommunityTools.Infrastructure;

/// <summary>
///     Fetches a source repository link anonymously and reports whether it answered.
///     <para>
///         The whole point is anonymity. A private repository answers perfectly well to the maker's
///         own browser and 404s to every player being invited to read it — so a check carrying any
///         credential would confirm exactly the wrong thing. No auth header, no cookies, and
///         redirects are followed because moving a repository between accounts is normal and leaves
///         a 301 behind.
///     </para>
///     <para>
///         What this proves is narrow and worth stating: the link resolves and is publicly readable.
///         It cannot prove the repository holds code, that the code is what is deployed, or that it
///         is a git repository at all. It catches dead links, typos and private repositories, which
///         is the failure surface that actually occurs.
///     </para>
/// </summary>
internal sealed class RepositoryReachabilityClient : IRepositoryReachabilityClient
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly HttpClient _client;

    public RepositoryReachabilityClient(HttpClient client)
    {
        _client = client;
    }

    public async Task<RepositoryReachability> Check(Uri repositoryUrl,
        CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);

        try
        {
            // GET rather than HEAD: several forges answer HEAD with 405 on a repository page that
            // serves a perfectly good 200 to a browser, and a maker cannot act on that difference.
            using var request = new HttpRequestMessage(HttpMethod.Get, repositoryUrl);
            using var response = await _client.SendAsync(request,
                HttpCompletionOption.ResponseHeadersRead, timeout.Token);

            var status = (int)response.StatusCode;
            if (response.IsSuccessStatusCode) return RepositoryReachability.Ok(status);

            return RepositoryReachability.Failed(
                response.StatusCode is >= HttpStatusCode.InternalServerError
                    ? WebhookFailureReason.ServerError
                    : WebhookFailureReason.ClientError,
                status);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // An HttpClient timeout surfaces as a cancellation rather than a TimeoutException.
            return RepositoryReachability.Failed(WebhookFailureReason.Timeout, null);
        }
        catch (HttpRequestException e)
        {
            return RepositoryReachability.Failed(Classify(e), null);
        }
    }

    private static WebhookFailureReason Classify(HttpRequestException e)
    {
        for (Exception? inner = e; inner is not null; inner = inner.InnerException)
            switch (inner)
            {
                case SocketException { SocketErrorCode: SocketError.HostNotFound or SocketError.NoData }:
                    return WebhookFailureReason.DnsFailure;
                case AuthenticationException:
                    return WebhookFailureReason.TlsFailure;
            }

        return WebhookFailureReason.InvalidResponse;
    }
}
