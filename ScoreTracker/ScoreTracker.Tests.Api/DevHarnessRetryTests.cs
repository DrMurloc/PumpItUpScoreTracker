using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using ScoreTracker.Data.DevTooling;
using Xunit;

namespace ScoreTracker.Tests.Api;

/// <summary>
///     What the local-dev harness does when a page request does not come back cleanly.
///     <para>
///         It pulls hundreds of pages with no way to resume, so the difference between retrying and
///         not is the difference between a slow sync and starting over from nothing. The judgment
///         worth pinning is which failures earn another attempt: a server that fell over does, and
///         a server that said "no" does not — a wrong API token is the most likely thing to go
///         wrong on this page, and making someone wait seven seconds to be told so is a worse
///         answer than telling them at once.
///     </para>
///     <para>
///         Drives <c>Page</c> directly over a stubbed handler. No server and no database, so it
///         belongs in a fast suite; it sits beside <see cref="DevHarnessRouteTests" /> because Data
///         grants this assembly the internals the harness is built from.
///     </para>
/// </summary>
public sealed class DevHarnessRetryTests
{
    private sealed record Row(string Name);

    /// <summary>Answers each request from a queued script and records what it was asked.</summary>
    private sealed class ScriptedHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpResponseMessage>> _script;

        public ScriptedHandler(params Func<HttpResponseMessage>[] script)
        {
            _script = new Queue<Func<HttpResponseMessage>>(script);
        }

        public List<string> Requested { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            // As a real handler does — otherwise a cancelled run still records a request here and
            // the cancellation test passes for the wrong reason.
            cancellationToken.ThrowIfCancellationRequested();
            Requested.Add(request.RequestUri!.PathAndQuery);
            return Task.FromResult(_script.Dequeue()());
        }
    }

    private static readonly Action<string> Ignored = _ => { };

    private static Func<HttpResponseMessage> Json(string body)
    {
        return () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) };
    }

    private static Func<HttpResponseMessage> Status(HttpStatusCode code)
    {
        return () => new HttpResponseMessage(code);
    }

    /// <summary>A 429 shaped like the one api/v2 sends: the wait, in whole seconds.</summary>
    private static Func<HttpResponseMessage> RateLimited(int seconds)
    {
        return () =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(seconds));
            return response;
        };
    }

    private static HttpClient ClientFor(ScriptedHandler handler)
    {
        return new HttpClient(handler) { BaseAddress = new Uri("https://example.invalid/") };
    }

    [Fact]
    public async Task AServerThatFellOverIsAskedAgainAndTheSyncSurvivesIt()
    {
        var handler = new ScriptedHandler(
            Status(HttpStatusCode.InternalServerError),
            Json("""{"data":[{"name":"Bad Apple"}],"next":null}"""));
        using var client = ClientFor(handler);

        var rows = await DevApiReader.Page<Row>(client, "api/v2/songs", Ignored, CancellationToken.None);

        Assert.Equal(new[] { "Bad Apple" }, rows.Select(r => r.Name));
        Assert.Equal(2, handler.Requested.Count);
    }

    [Fact]
    public async Task AWrongTokenFailsOnTheFirstAttemptRatherThanAfterEveryRetry()
    {
        var handler = new ScriptedHandler(Status(HttpStatusCode.Unauthorized));
        using var client = ClientFor(handler);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            DevApiReader.Page<Row>(client, "api/v2/songs", Ignored, CancellationToken.None));

        Assert.Single(handler.Requested);
    }

    [Fact]
    public async Task AListThisMixDoesNotPublishIsAnEmptyResultRatherThanAFailedSync()
    {
        var handler = new ScriptedHandler(Status(HttpStatusCode.NotFound));
        using var client = ClientFor(handler);

        var rows = await DevApiReader.Page<Row>(client, "api/v2/tier-lists/pg-difficulty",
            Ignored, CancellationToken.None, true);

        Assert.Empty(rows);
        Assert.Single(handler.Requested);
    }

    [Fact]
    public async Task TheCursorIsFollowedToTheEndAndUsedExactlyAsGiven()
    {
        var handler = new ScriptedHandler(
            Json("""{"data":[{"name":"Bad Apple"}],"next":"api/v2/songs?cursor=abc%3D"}"""),
            Json("""{"data":[{"name":"Vook"}],"next":null}"""));
        using var client = ClientFor(handler);

        var rows = await DevApiReader.Page<Row>(client, "api/v2/songs", Ignored, CancellationToken.None);

        Assert.Equal(new[] { "Bad Apple", "Vook" }, rows.Select(r => r.Name));
        Assert.Equal(new[] { "/api/v2/songs", "/api/v2/songs?cursor=abc%3D" }, handler.Requested);
    }

    [Fact]
    public async Task TheRateLimitIsWaitedOutRatherThanFailingTheSync()
    {
        var handler = new ScriptedHandler(
            RateLimited(0),
            Json("""{"data":[{"name":"Bad Apple"}],"next":null}"""));
        using var client = ClientFor(handler);

        var rows = await DevApiReader.Page<Row>(client, "api/v2/songs", Ignored, CancellationToken.None);

        Assert.Equal(new[] { "Bad Apple" }, rows.Select(r => r.Name));
        Assert.Equal(2, handler.Requested.Count);
    }

    [Fact]
    public async Task WaitingOnTheRateLimitSaysSoRatherThanLookingLikeAHang()
    {
        var announced = new List<string>();
        var handler = new ScriptedHandler(
            RateLimited(0),
            Json("""{"data":[],"next":null}"""));
        using var client = ClientFor(handler);

        await DevApiReader.Page<Row>(client, "api/v2/songs", announced.Add, CancellationToken.None);

        Assert.Contains(announced, m => m.Contains("Rate limited", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ARateLimitThatNeverClearsGivesUpInsteadOfWaitingForever()
    {
        var handler = new ScriptedHandler(Enumerable.Repeat(RateLimited(0), 4).ToArray());
        using var client = ClientFor(handler);

        var thrown = await Assert.ThrowsAsync<HttpRequestException>(() =>
            DevApiReader.Page<Row>(client, "api/v2/songs", Ignored, CancellationToken.None));

        Assert.Equal(HttpStatusCode.TooManyRequests, thrown.StatusCode);
        // The four the wait budget allows, and no fifth from the transient-retry path on top.
        Assert.Equal(4, handler.Requested.Count);
    }

    [Fact]
    public async Task ACancelledRunStopsInsteadOfRetryingItsWayThroughTheBackoff()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        var handler = new ScriptedHandler(Status(HttpStatusCode.InternalServerError));
        using var client = ClientFor(handler);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            DevApiReader.Page<Row>(client, "api/v2/songs", Ignored, cancelled.Token));

        Assert.Empty(handler.Requested);
    }
}
