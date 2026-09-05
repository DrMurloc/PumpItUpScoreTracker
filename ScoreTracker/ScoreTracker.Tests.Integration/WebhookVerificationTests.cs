using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Options;
using ScoreTracker.CommunityTools.Contracts;
using ScoreTracker.CommunityTools.Domain;
using ScoreTracker.CommunityTools.Infrastructure;
using ScoreTracker.CommunityTools.Wiring;
using ScoreTracker.Data.Clients;
using ScoreTracker.Data.Configuration;
using ScoreTracker.Tests.Integration.Fixtures;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace ScoreTracker.Tests.Integration;

/// <summary>
///     Verification against a real HTTP listener: what an endpoint has to do to prove it belongs to
///     the maker who configured it.
///     <para>
///         The property under test is knowledge, not reachability. The earlier scheme POSTed a
///         challenge and accepted it echoed back, which anything able to receive our request could
///         satisfy — including whatever a hijacked DNS record points at. The request now carries no
///         answer, and the endpoint has to already hold the secret.
///     </para>
/// </summary>
[Collection(IntegrationTestCollection.Name)]
[ExcludeFromCodeCoverage]
public sealed class WebhookVerificationTests : IAsyncLifetime, IDisposable
{
    private const string Secret = "vfy_the-makers-own-secret";
    private static readonly Guid ToolId = Guid.Parse("eeeeeeee-0000-0000-0000-00000000000e");

    private readonly SqlServerFixture _fixture;
    private readonly WireMockServer _maker = WireMockServer.Start();

    private readonly ToolSecretProtector _protector = new(new KeyEnvelope(
        Options.Create(new KeyVaultConfiguration
        {
            LocalKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
        })));

    public WebhookVerificationTests(SqlServerFixture fixture)
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

    private EFToolSecretReader Secrets => new(_fixture.DbContextFactory, _protector);

    private WebhookDeliveryClient Client => new(new HttpClient(), Options.Create(new CommunityToolsConfiguration()));

    private void EndpointAnswers(string body)
    {
        _maker.Given(Request.Create().WithPath("/hook").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody(body));
    }

    private Task<WebhookVerificationOutcome> Verify()
    {
        return Client.Verify(Hook, WebhookSecrets.HashOf(Secret), "X-Planner-Token", "s3cret",
            CancellationToken.None);
    }

    [Fact]
    public async Task AnEndpointThatKnowsTheSecretVerifies()
    {
        EndpointAnswers(Secret);

        Assert.True((await Verify()).Succeeded);
    }

    [Fact]
    public async Task SoDoesOneThatWrapsItInJson()
    {
        EndpointAnswers($"{{\"secret\":\"{Secret}\"}}");

        Assert.True((await Verify()).Succeeded);
    }

    /// <summary>
    ///     The regression that motivated all of this. A handler that mirrors whatever it was sent
    ///     used to pass, because we were sending it the answer. Nothing in the request is the
    ///     answer any more, so mirroring proves only that it can mirror.
    /// </summary>
    [Fact]
    public async Task AnEndpointThatMirrorsOurRequestDoesNotVerify()
    {
        _maker.Given(Request.Create().WithPath("/hook").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200)
                .WithBody(request => request.Body ?? string.Empty));

        var outcome = await Verify();

        Assert.False(outcome.Succeeded);
        Assert.Equal(WebhookFailureReason.InvalidResponse, outcome.Reason);
    }

    /// <summary>
    ///     Nothing we send may be sufficient to answer with — otherwise an attacker who receives the
    ///     request has been handed the proof. This pins the request body and the header value, which
    ///     are the only two things that reach the endpoint.
    /// </summary>
    [Theory]
    [InlineData("{\"type\":\"url_verification\"}")]
    [InlineData("url_verification")]
    [InlineData("s3cret")]
    [InlineData("OK")]
    [InlineData("")]
    public async Task NothingWeSendIsAnAcceptableAnswer(string body)
    {
        EndpointAnswers(body);

        Assert.False((await Verify()).Succeeded);
    }

    /// <summary>The header still goes, so the maker's server can tell our call from anyone else's.</summary>
    [Fact]
    public async Task TheVerificationCallCarriesTheOutboundHeaderAndNoChallenge()
    {
        EndpointAnswers(Secret);

        await Verify();

        var sent = _maker.LogEntries.Single().RequestMessage;
        Assert.Equal("s3cret", sent.Headers!["X-Planner-Token"].Single());
        Assert.DoesNotContain("challenge", sent.Body!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Secret, sent.Body!, StringComparison.Ordinal);
    }

    /// <summary>
    ///     The round trip through a real column. The header is written encrypted and read back
    ///     decrypted; the verification secret is written hashed and never comes back at all.
    /// </summary>
    [Fact]
    public async Task TheHeaderSurvivesEncryptionAndTheSecretNeverComesBack()
    {
        await new EFToolRepository(_fixture.DbContextFactory).Save(
            Tool.Create(ToolId, Guid.NewGuid(), SharedKernel.ValueTypes.Name.From("Planner"),
                DateTimeOffset.UtcNow));

        await Secrets.SetOutboundHeader(ToolId, "X-Planner-Token", "s3cret");
        await Secrets.SetVerificationSecretHash(ToolId, WebhookSecrets.HashOf(Secret));

        var (name, value) = await Secrets.GetOutboundHeader(ToolId);
        Assert.Equal("X-Planner-Token", name);
        Assert.Equal("s3cret", value);

        var hash = await Secrets.GetVerificationSecretHash(ToolId);
        Assert.Equal(WebhookSecrets.HashOf(Secret), hash);
        Assert.DoesNotContain(Secret, hash!, StringComparison.OrdinalIgnoreCase);

        // What the database actually holds for the header is not the header.
        await using var database = await _fixture.DbContextFactory.CreateDbContextAsync();
        await using var command = database.Database.GetDbConnection().CreateCommand();
        await database.Database.OpenConnectionAsync();
        command.CommandText = $"SELECT OutboundHeaderValue FROM scores.Tool WHERE Id = '{ToolId}'";
        var stored = (string?)await command.ExecuteScalarAsync();
        Assert.NotNull(stored);
        Assert.DoesNotContain("s3cret", stored, StringComparison.Ordinal);
    }
}
