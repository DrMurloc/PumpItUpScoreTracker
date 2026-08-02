using ScoreTracker.CommunityTools.Contracts;
using ScoreTracker.CommunityTools.Domain;
using ScoreTracker.CommunityTools.Infrastructure;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace ScoreTracker.Tests.Integration;

/// <summary>
///     The outbound webhook client, against a real HTTP listener.
///     <para>
///         Everything else in the delivery pipeline is decided by our own code and is covered with
///         mocked ports. This class is the opposite: its whole job is classifying what somebody
///         else's server did, and a mocked <c>HttpClient</c> would only prove we can spell the
///         status codes we invented. The maker-facing console shows this vocabulary and nothing
///         else, so getting it wrong means telling a maker their endpoint timed out when it 500'd.
///     </para>
///     <para>
///         No database, no Testcontainers — this one needs a socket, not SQL.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class WebhookDeliveryClientTests : IDisposable
{
    private readonly WireMockServer _maker = WireMockServer.Start();

    public void Dispose()
    {
        _maker.Stop();
    }

    private static WebhookDeliveryClient Build()
    {
        // The real timeout is ten seconds; the client owns it, so the tests that need it to fire
        // wait it out rather than reaching in to shorten it.
        return new WebhookDeliveryClient(new HttpClient());
    }

    private Uri Hook(string path)
    {
        return new Uri($"{_maker.Urls[0].TrimEnd('/')}/{path}");
    }

    [Fact]
    public async Task ASuccessCarriesTheStatusCodeAndTheHeadersArriveOnTheWire()
    {
        _maker.Given(Request.Create().WithPath("/hook").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(202));

        var outcome = await Build().Post(Hook("hook"), "{\"a\":1}", "d-123",
            "X-Planner-Token", "s3cret", CancellationToken.None);

        Assert.True(outcome.Succeeded);
        Assert.Equal(202, outcome.StatusCode);

        var sent = _maker.LogEntries.Single().RequestMessage;
        Assert.Equal("d-123", sent.Headers![WebhookHeaders.DeliveryId].Single());
        Assert.Equal("s3cret", sent.Headers["X-Planner-Token"].Single());
        Assert.Equal("{\"a\":1}", sent.Body);
    }

    // A maker who configured no header must still get the delivery — the header is only mandatory
    // in session mode, and an empty header name must not become an empty header.
    [Fact]
    public async Task NoConfiguredHeaderSendsNoExtraHeader()
    {
        _maker.Given(Request.Create().WithPath("/hook").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200));

        var outcome = await Build().Post(Hook("hook"), "{}", "d-1", null, null, CancellationToken.None);

        Assert.True(outcome.Succeeded);
    }

    [Theory]
    [InlineData(400, WebhookFailureReason.ClientError)]
    [InlineData(401, WebhookFailureReason.ClientError)]
    [InlineData(404, WebhookFailureReason.ClientError)]
    [InlineData(500, WebhookFailureReason.ServerError)]
    [InlineData(502, WebhookFailureReason.ServerError)]
    public async Task TheStatusCodeDecidesWhoseFaultItWas(int status, WebhookFailureReason expected)
    {
        _maker.Given(Request.Create().WithPath("/hook").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(status).WithBody("nope"));

        var outcome = await Build().Post(Hook("hook"), "{}", "d-1", null, null, CancellationToken.None);

        Assert.False(outcome.Succeeded);
        Assert.Equal(expected, outcome.Reason);
        Assert.Equal(status, outcome.StatusCode);
        // The remote's own words, which is what a maker can act on.
        Assert.Equal("nope", outcome.RemoteBodySnippet);
    }

    /// <summary>
    ///     The console shows this snippet. An endpoint that answers with a stack trace or an HTML
    ///     error page would otherwise put arbitrary kilobytes into a table and onto a screen.
    /// </summary>
    [Fact]
    public async Task AHugeErrorBodyIsTruncated()
    {
        _maker.Given(Request.Create().WithPath("/hook").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(500).WithBody(new string('x', 5000)));

        var outcome = await Build().Post(Hook("hook"), "{}", "d-1", null, null, CancellationToken.None);

        Assert.Equal(500, outcome.RemoteBodySnippet!.Length);
    }

    /// <summary>
    ///     An HttpClient timeout surfaces as a cancellation, not a TimeoutException, and the only
    ///     thing separating "we gave up" from "the caller gave up" is which token fired. Reporting
    ///     it as anything else sends a maker looking at the wrong problem.
    /// </summary>
    [Fact]
    public async Task AHangingEndpointIsATimeoutNotAnUnclassifiedFailure()
    {
        _maker.Given(Request.Create().WithPath("/hook").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200).WithDelay(TimeSpan.FromSeconds(15)));

        var outcome = await Build().Post(Hook("hook"), "{}", "d-1", null, null, CancellationToken.None);

        Assert.False(outcome.Succeeded);
        Assert.Equal(WebhookFailureReason.Timeout, outcome.Reason);
        Assert.Null(outcome.StatusCode);
    }

    [Fact]
    public async Task AHostThatDoesNotResolveIsADnsFailure()
    {
        var outcome = await Build().Post(
            new Uri("https://this-host-does-not-exist.piuscores-test.invalid/hook"), "{}", "d-1",
            null, null, CancellationToken.None);

        Assert.False(outcome.Succeeded);
        Assert.Equal(WebhookFailureReason.DnsFailure, outcome.Reason);
    }

    // The caller's own cancellation is not a timeout — a shutdown mid-delivery must not be logged
    // to a maker as their endpoint being slow.
    [Fact]
    public async Task TheCallersCancellationIsNotReportedAsATimeout()
    {
        _maker.Given(Request.Create().WithPath("/hook").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200).WithDelay(TimeSpan.FromSeconds(15)));
        using var cancelled = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Build().Post(Hook("hook"), "{}", "d-1", null, null, cancelled.Token));
    }

    [Fact]
    public async Task AnEndpointThatEchoesTheChallengeVerifies()
    {
        _maker.Given(Request.Create().WithPath("/verify").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody("vfy_abc123"));

        var outcome = await Build().Verify(Hook("verify"), "vfy_abc123", null, null,
            CancellationToken.None);

        Assert.True(outcome.Succeeded);
    }

    /// <summary>
    ///     The interesting failure: the URL is alive but it is not the maker's handler. Reporting it
    ///     with the body they actually sent is what turns it from a mystery into a fix.
    /// </summary>
    [Fact]
    public async Task A200ThatDoesNotEchoIsAnInvalidResponseWithTheBody()
    {
        _maker.Given(Request.Create().WithPath("/verify").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody("Hello from nginx"));

        var outcome = await Build().Verify(Hook("verify"), "vfy_abc123", null, null,
            CancellationToken.None);

        Assert.False(outcome.Succeeded);
        Assert.Equal(WebhookFailureReason.InvalidResponse, outcome.Reason);
        Assert.Equal("Hello from nginx", outcome.RemoteBodySnippet);
    }

    // Verification sends the header too, so a handler that rejects unauthenticated calls can be
    // verified at all — without this, requiring the header would make verification impossible.
    [Fact]
    public async Task VerificationCarriesTheOutboundHeader()
    {
        _maker.Given(Request.Create().WithPath("/verify").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody("vfy_abc123"));

        await Build().Verify(Hook("verify"), "vfy_abc123", "X-Planner-Token", "s3cret",
            CancellationToken.None);

        Assert.Equal("s3cret",
            _maker.LogEntries.Single().RequestMessage.Headers!["X-Planner-Token"].Single());
    }

    [Fact]
    public async Task AVerificationAgainstADeadHostFailsWithoutThrowing()
    {
        var outcome = await Build().Verify(
            new Uri("https://this-host-does-not-exist.piuscores-test.invalid/verify"), "vfy_abc123",
            null, null, CancellationToken.None);

        Assert.False(outcome.Succeeded);
        Assert.Equal(WebhookFailureReason.DnsFailure, outcome.Reason);
    }
}
