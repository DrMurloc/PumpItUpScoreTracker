using System.Security.Cryptography;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using ScoreTracker.Data.Clients;
using ScoreTracker.Data.Configuration;
using ScoreTracker.CommunityTools.Application;
using ScoreTracker.CommunityTools.Contracts;
using ScoreTracker.CommunityTools.Domain;
using ScoreTracker.CommunityTools.Infrastructure;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.Integration.Fixtures;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace ScoreTracker.Tests.Integration;

/// <summary>
///     The delivery pipeline end to end: a real database underneath, a real HTTP listener standing
///     in for the maker, and every link in between running for real.
///     <para>
///         Each link is covered in isolation elsewhere. What nothing covered is the wiring — the
///         dispatcher writing a queue row before it attempts, the client posting it, the outcome
///         landing back on that row, and the retry sweep picking up what failed. Those are the seams
///         where a mocked port proves only that we called something.
///     </para>
/// </summary>
[Collection(IntegrationTestCollection.Name)]
[ExcludeFromCodeCoverage]
public sealed class WebhookDeliveryChainTests : IAsyncLifetime, IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 8, 2, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid ToolId = Guid.Parse("aaaaaaaa-0000-0000-0000-00000000000a");
    private static readonly Guid PlayerId = Guid.Parse("bbbbbbbb-0000-0000-0000-00000000000b");

    private readonly SqlServerFixture _fixture;
    private readonly WireMockServer _maker = WireMockServer.Start();

    public WebhookDeliveryChainTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync()
    {
        return _fixture.ResetAsync();
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _maker.Stop();
    }

    private Uri Hook => new($"{_maker.Urls[0].TrimEnd('/')}/hook");

    private EFToolRepository Tools => new(_fixture.DbContextFactory);

    private EFWebhookDeliveryRepository Deliveries => new(_fixture.DbContextFactory);

    /// <summary>
    ///     The real protector over a local-key envelope, not a stub: the header is written encrypted
    ///     and read back decrypted, so these tests prove the round trip survives the database as
    ///     well as the wire. One key for the whole class, because a fresh one per call could not
    ///     decrypt what the previous one wrote.
    /// </summary>
    private readonly ToolSecretProtector _protector = new(new KeyEnvelope(
        Options.Create(new KeyVaultConfiguration
        {
            LocalKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
        })));

    private EFToolSecretReader Secrets => new(_fixture.DbContextFactory, _protector);

    private WebhookDeliveryDispatcher Dispatcher()
    {
        return new WebhookDeliveryDispatcher(Deliveries, new WebhookDeliveryClient(new HttpClient()),
            Tools, Secrets,
            Mock.Of<IDateTimeOffsetAccessor>(d => d.Now == Now));
    }

    /// <summary>A verified score-push tool with the maker's header configured.</summary>
    private async Task<Tool> GivenAConnectedTool()
    {
        var tool = Tool.Create(ToolId, Guid.NewGuid(), Name.From("Planner"), Now);
        tool.SetWebhook(WebhookMode.ScorePush, Hook, 0, hasOutboundHeader: true);
        tool.MarkWebhookVerified(Now);
        await Tools.Save(tool);
        await Secrets
            .SetOutboundHeader(ToolId, "X-Planner-Token", "s3cret");
        return tool;
    }

    private static DeliveryPayload.PlayerBlock Player()
    {
        return new DeliveryPayload.PlayerBlock("Phoenix", "phoenix", PlayerId, "DrMurloc", "MURLOC#1");
    }

    private static DeliveryPayload.Change[] OneChange()
    {
        return new[]
        {
            new DeliveryPayload.Change(Guid.Parse("cccccccc-0000-0000-0000-00000000000c"),
                false, 990_000, 999_231, null, null, "PerfectGame", false)
        };
    }

    [Fact]
    public async Task ADeliveryReachesTheMakerAndTheQueueRecordsIt()
    {
        _maker.Given(Request.Create().WithPath("/hook").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200));
        var tool = await GivenAConnectedTool();

        await Dispatcher().Dispatch(tool, Player(), null, OneChange(), false, false,
            CancellationToken.None);

        var sent = _maker.LogEntries.Single().RequestMessage;
        // The maker's own header, verbatim — the whole of how they know it is us.
        Assert.Equal("s3cret", sent.Headers!["X-Planner-Token"].Single());
        Assert.Contains("MURLOC#1", sent.Body);
        Assert.Contains("999231", sent.Body);

        var row = (await Deliveries.GetForTool(ToolId, 10)).Single();
        Assert.Equal(DeliveryStatus.Succeeded, row.Status);
        Assert.Equal(200, row.RemoteStatusCode);
    }

    /// <summary>
    ///     Written before it is attempted. The bus is in-memory, so a delivery that is only in
    ///     flight is a delivery that dies with the process, with no trace and no retry.
    /// </summary>
    [Fact]
    public async Task AFailedDeliveryIsQueuedWithItsBodyAndAnAttemptTime()
    {
        _maker.Given(Request.Create().WithPath("/hook").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(500).WithBody("boom"));
        var tool = await GivenAConnectedTool();

        await Dispatcher().Dispatch(tool, Player(), null, OneChange(), false, false,
            CancellationToken.None);

        var row = (await Deliveries.GetForTool(ToolId, 10)).Single();
        Assert.Equal(DeliveryStatus.Failed, row.Status);
        Assert.Equal(WebhookFailureReason.ServerError, row.FailureReason);
        Assert.Equal("boom", row.RemoteBodySnippet);
        Assert.NotNull(row.NextAttemptAt);
        // Kept, because it is the thing a retry re-sends.
        Assert.NotNull(row.Body);
    }

    /// <summary>
    ///     The sweep is what makes the queue durable rather than decorative: a maker who deploys
    ///     over a delivery gets it on the next pass instead of losing it.
    /// </summary>
    [Fact]
    public async Task TheRetrySweepReDeliversWhatFailed()
    {
        _maker.Given(Request.Create().WithPath("/hook").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(503));
        var tool = await GivenAConnectedTool();
        await Dispatcher().Dispatch(tool, Player(), null, OneChange(), false, false,
            CancellationToken.None);

        // Their deploy finishes.
        _maker.Reset();
        _maker.Given(Request.Create().WithPath("/hook").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200));

        var due = await Deliveries.GetDue(Now.AddHours(1), 50, Now.AddHours(2));
        var delivery = Assert.Single(due);
        Assert.True(await Dispatcher().Attempt(delivery.Id, CancellationToken.None));

        var row = (await Deliveries.GetForTool(ToolId, 10)).Single();
        Assert.Equal(DeliveryStatus.Succeeded, row.Status);
        Assert.Equal(2, row.Attempt);
    }

    /// <summary>
    ///     A maker who repoints their URL mid-backoff has un-verified it. The queued body must not
    ///     follow them somewhere nobody has proven they own.
    /// </summary>
    [Fact]
    public async Task ARetryStopsIfTheUrlChangedAndWasNotReVerified()
    {
        _maker.Given(Request.Create().WithPath("/hook").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(500));
        var tool = await GivenAConnectedTool();
        await Dispatcher().Dispatch(tool, Player(), null, OneChange(), false, false,
            CancellationToken.None);

        tool.SetWebhook(WebhookMode.ScorePush, new Uri("https://elsewhere.example/hook"), 0,
            hasOutboundHeader: true);
        await Tools.Save(tool);

        var delivery = Assert.Single(await Deliveries.GetDue(Now.AddHours(1), 50, Now.AddHours(2)));
        Assert.False(await Dispatcher().Attempt(delivery.Id, CancellationToken.None));
    }

    /// <summary>
    ///     The rule with the worst failure mode in the vertical, exercised against a real database
    ///     and a real socket: a session delivery goes out and leaves nothing behind.
    /// </summary>
    [Fact]
    public async Task ASessionDeliveryIsSentAndNeverWrittenDown()
    {
        _maker.Given(Request.Create().WithPath("/hook").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200));

        var tool = Tool.Create(ToolId, Guid.NewGuid(), Name.From("Tracker"), Now);
        tool.SetWebhook(WebhookMode.PiuGameSession, Hook, 0, hasOutboundHeader: true);
        tool.MarkWebhookVerified(Now);
        await Tools.Save(tool);
        await Tools.GrantShare(ToolId, PlayerId, ShareSource.Direct, Now);

        var client = new SessionDeliveryClient(Tools, new WebhookDeliveryClient(new HttpClient()),
            Secrets,
            new EFToolActivityRepository(_fixture.DbContextFactory),
            Mock.Of<IDateTimeOffsetAccessor>(d => d.Now == Now),
            NullLogger<SessionDeliveryClient>.Instance);

        await client.DeliverSession(PlayerId, MixEnum.Phoenix, RedactedString.From("sid-live-abc"),
            "MURLOC#1");

        // It went.
        Assert.Contains("sid-live-abc", _maker.LogEntries.Single().RequestMessage.Body);
        // And nothing about it is in the queue — no row, so no body, so nothing to leak.
        Assert.Empty(await Deliveries.GetForTool(ToolId, 10));
    }

    /// <summary>
    ///     The sweep claims what it takes. Without it, a sweep that runs longer than its 5-minute
    ///     cadence — which is exactly what happens when endpoints are dead and each burns the ten
    ///     second timeout — overlaps the next one on the same rows and we generate the duplicates
    ///     ourselves.
    /// </summary>
    [Fact]
    public async Task ASecondSweepFindsNothingWhileTheFirstStillHoldsTheRows()
    {
        _maker.Given(Request.Create().WithPath("/hook").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(500));
        var tool = await GivenAConnectedTool();
        await Dispatcher().Dispatch(tool, Player(), null, OneChange(), false, false,
            CancellationToken.None);

        var first = await Deliveries.GetDue(Now.AddHours(1), 50, Now.AddHours(2));
        Assert.Single(first);

        var second = await Deliveries.GetDue(Now.AddHours(1), 50, Now.AddHours(2));
        Assert.Empty(second);
    }
}
