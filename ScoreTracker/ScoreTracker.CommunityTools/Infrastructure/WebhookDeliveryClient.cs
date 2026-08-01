using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;
using ScoreTracker.CommunityTools.Contracts;
using ScoreTracker.CommunityTools.Domain;

namespace ScoreTracker.CommunityTools.Infrastructure;

/// <summary>
///     POSTs a delivery to a maker's endpoint and classifies what came back.
///     <para>
///         The classification is the point. A maker-facing activity log is not an admin page, so raw
///         exception text has no business there — a closed reason vocabulary plus the remote's own
///         status code says everything a maker needs and nothing about our internals.
///     </para>
/// </summary>
internal sealed class WebhookDeliveryClient : IWebhookDeliveryClient
{
    /// <summary>
    ///     Ten seconds. Long enough for a cold serverless start, short enough that a hung endpoint
    ///     does not hold a worker while the next import queues behind it.
    /// </summary>
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly HttpClient _client;

    public WebhookDeliveryClient(HttpClient client)
    {
        _client = client;
    }

    public async Task<WebhookDeliveryOutcome> Post(Uri url, string body, string deliveryId,
        string signature, string? headerName, string? headerValue, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        request.Headers.TryAddWithoutValidation(WebhookSigning.SignatureHeader, signature);
        request.Headers.TryAddWithoutValidation(WebhookSigning.DeliveryIdHeader, deliveryId);
        if (!string.IsNullOrWhiteSpace(headerName) && headerValue is not null)
            request.Headers.TryAddWithoutValidation(headerName, headerValue);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var response = await _client.SendAsync(request, timeout.Token);
            stopwatch.Stop();

            var snippet = await ReadSnippet(response, cancellationToken);
            if (response.IsSuccessStatusCode)
                return WebhookDeliveryOutcome.Success((int)response.StatusCode, (int)stopwatch.ElapsedMilliseconds);

            var reason = (int)response.StatusCode >= 500
                ? WebhookFailureReason.ServerError
                : WebhookFailureReason.ClientError;
            return WebhookDeliveryOutcome.Failure(reason, (int)response.StatusCode, snippet,
                (int)stopwatch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // An HttpClient timeout surfaces as a cancellation, not a TimeoutException — the
            // distinction between "we gave up" and "the caller gave up" is the outer token.
            stopwatch.Stop();
            return WebhookDeliveryOutcome.Failure(WebhookFailureReason.Timeout, null, null,
                (int)stopwatch.ElapsedMilliseconds);
        }
        catch (HttpRequestException e)
        {
            stopwatch.Stop();
            return WebhookDeliveryOutcome.Failure(Classify(e), null, null, (int)stopwatch.ElapsedMilliseconds);
        }
    }

    /// <summary>
    ///     Maps a transport failure onto the maker-facing vocabulary. "Couldn't reach your server"
    ///     and "your TLS is broken" are different problems, and a maker can act on the difference.
    /// </summary>
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

    private static async Task<string?> ReadSnippet(HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            var text = await response.Content.ReadAsStringAsync(cancellationToken);
            return string.IsNullOrWhiteSpace(text) ? null : text[..Math.Min(text.Length, 500)];
        }
        catch (Exception)
        {
            // The body is a nicety for the console; failing to read it must not turn a classified
            // failure into an unclassified one.
            return null;
        }
    }
}

/// <summary>What one POST attempt produced.</summary>
[ExcludeFromCodeCoverage]
internal sealed record WebhookDeliveryOutcome(
    bool Succeeded,
    WebhookFailureReason Reason,
    int? StatusCode,
    string? RemoteBodySnippet,
    int LatencyMs)
{
    public static WebhookDeliveryOutcome Success(int statusCode, int latencyMs)
    {
        return new WebhookDeliveryOutcome(true, WebhookFailureReason.None, statusCode, null, latencyMs);
    }

    public static WebhookDeliveryOutcome Failure(WebhookFailureReason reason, int? statusCode,
        string? snippet, int latencyMs)
    {
        return new WebhookDeliveryOutcome(false, reason, statusCode, snippet, latencyMs);
    }
}

internal interface IWebhookDeliveryClient
{
    Task<WebhookDeliveryOutcome> Post(Uri url, string body, string deliveryId, string signature,
        string? headerName, string? headerValue, CancellationToken cancellationToken);
}
