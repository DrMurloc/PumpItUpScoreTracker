using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.OfficialMirror.Infrastructure.Apis;

namespace ScoreTracker.Tests.Integration.LiveSite;

/// <summary>
///     Shares one real PiuGameApi (and at most one login) across the live-site tests.
///     Login is lazy: skipped tests never touch the network, and the whole class produces
///     exactly one login POST per run no matter how many authenticated tests execute —
///     polite to the real site and to the account.
/// </summary>
public sealed class PiuGameSessionFixture : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly HttpClient _publicClient;
    private HttpClient? _authenticatedClient;

    public PiuGameSessionFixture()
    {
        // Mirrors the typed-client registration in OfficialMirrorRegistrationExtensions.
        _publicClient = new HttpClient();
        _publicClient.DefaultRequestHeaders.Add("Origin", "https://phoenix.piugame.com");
        Api = new PiuGameApi(_publicClient, NullLogger<PiuGameApi>.Instance, Mock.Of<ICurrentUserAccessor>());
    }

    internal PiuGameApi Api { get; }

    public string? SessionId { get; private set; }

    // This project shares the Aspire AppHost's UserSecretsId (see the csproj), so credentials
    // configured once for local dev also drive these tests:
    //   dotnet user-secrets set "PiuTest:Username" "..." --project ScoreTracker/ScoreTracker.AppHost
    // Environment variables (PIU_TEST_USERNAME / PIU_TEST_PASSWORD) still win when both are set.
    private static readonly Lazy<IConfigurationRoot> Configuration = new(() =>
        new ConfigurationBuilder()
            .AddUserSecrets<PiuGameSessionFixture>(optional: true)
            .Build());

    public static string? Username =>
        Environment.GetEnvironmentVariable("PIU_TEST_USERNAME") ?? Configuration.Value["PiuTest:Username"];

    public static string? Password =>
        Environment.GetEnvironmentVariable("PIU_TEST_PASSWORD") ?? Configuration.Value["PiuTest:Password"];

    public static bool CredentialsConfigured =>
        !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(Password);

    public void Dispose()
    {
        _publicClient.Dispose();
        _authenticatedClient?.Dispose();
        _gate.Dispose();
    }

    internal async Task<HttpClient> GetAuthenticatedClient(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_authenticatedClient is not null) return _authenticatedClient;

            var (client, sid) = await Api.GetSessionId(Username!, Password!, cancellationToken);
            Assert.False(string.IsNullOrWhiteSpace(sid),
                "Login against the live site produced no session id — the PIU login flow has changed shape.");
            SessionId = sid;
            _authenticatedClient = client;
            return client;
        }
        finally
        {
            _gate.Release();
        }
    }
}
