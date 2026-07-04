using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Playwright;
using Respawn;
using ScoreTracker.CompositionRoot;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Domain.SecondaryPorts;
using Testcontainers.MsSql;
using WireMock.Server;

namespace ScoreTracker.Tests.E2E.Support;

/// <summary>
///     The whole E2E stack, booted once per run (collection fixture):
///     an ephemeral SQL Server (Testcontainers), a WireMock stub answering as
///     phoenix.piugame.com, the REAL web app hosted on Kestrel via
///     WebApplicationFactory (real MassTransit in-memory bus, real Hangfire, real
///     migrations via AutoMigrate), and a headless Playwright Chromium.
///     Between tests, <see cref="ResetDatabaseAsync" /> respawns the scores schema
///     and flushes the app's memory cache.
/// </summary>
public sealed class E2EAppFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _sqlContainer = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2025-latest")
        .Build();

    private WebApplicationFactory<Program>? _factory;
    private IPlaywright? _playwright;
    private Respawner? _respawner;

    public WireMockServer PiuGame { get; private set; } = null!;
    public IBrowser Browser { get; private set; } = null!;
    public string BaseUrl { get; private set; } = string.Empty;
    public IDbContextFactory<ChartAttemptDbContext> DbContextFactory { get; private set; } = null!;
    public E2ESeedData Seed { get; private set; } = null!;
    public string ConnectionString => _sqlContainer.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _sqlContainer.StartAsync();

        PiuGame = WireMockServer.Start();
        PiuGame.MapPiuGameSite();

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("SQL:ConnectionString", _sqlContainer.GetConnectionString());
            builder.UseSetting("AutoMigrate", "true");
            builder.UseSetting("PreventRecurringJobs", "true");
            builder.UseSetting("DevAuth:Enabled", "true");
            builder.UseSetting("PiuGame:BaseUrl", PiuGame.Urls[0]);
            builder.UseSetting("PiuGame:UcsBaseUrl", PiuGame.Urls[0]);
            builder.UseSetting("PiuGame:AmPassUrl", PiuGame.Urls[0]);
            // The OAuth handlers validate ClientId/Secret on first scheme resolution and the
            // Development placeholders are empty strings. Tests never OAuth-challenge, so
            // dummies keep the handlers registered (like production) without a 500 on render.
            builder.UseSetting("Discord:ClientId", "e2e-dummy");
            builder.UseSetting("Discord:ClientSecret", "e2e-dummy");
            builder.UseSetting("Google:ClientId", "e2e-dummy");
            builder.UseSetting("Google:ClientSecret", "e2e-dummy");
            builder.UseSetting("Facebook:AppId", "e2e-dummy");
            builder.UseSetting("Facebook:AppSecret", "e2e-dummy");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IFileUploadClient>();
                services.AddSingleton<IFileUploadClient, FakeFileUploadClient>();
            });
        });
        _factory.UseKestrel();
        _factory.StartServer();
        var addresses = _factory.Services.GetRequiredService<IServer>().Features
                            .Get<IServerAddressesFeature>()?.Addresses
                        ?? throw new InvalidOperationException("Kestrel exposed no server addresses.");
        BaseUrl = addresses.First().Replace("[::]", "127.0.0.1").Replace("0.0.0.0", "127.0.0.1").TrimEnd('/');

        var contextOptions = new DbContextOptionsBuilder<ChartAttemptDbContext>()
            .UseSqlServer(_sqlContainer.GetConnectionString())
            .Options;
        DbContextFactory = new TestDbContextFactory(contextOptions);
        Seed = new E2ESeedData(DbContextFactory);

        // The app's startup (AutoMigrate) created the schema; Respawn can now snapshot it.
        await using (var connection = new SqlConnection(_sqlContainer.GetConnectionString()))
        {
            await connection.OpenAsync();
            _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
            {
                DbAdapter = DbAdapter.SqlServer,
                SchemasToInclude = new[] { "scores" }
            });
        }

        // No-op when the browser is already cached; first run downloads Chromium.
        var exitCode = Microsoft.Playwright.Program.Main(new[] { "install", "chromium" });
        if (exitCode != 0)
            throw new InvalidOperationException($"`playwright install chromium` failed with exit code {exitCode}.");

        _playwright = await Playwright.CreateAsync();
        Browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
    }

    /// <summary>
    ///     Wipes the scores schema, flushes the app's memory cache, and restores the
    ///     happy-path PiuGame stubs. The cache flush matters: the app caches aggressively
    ///     (tier lists a day, charts two weeks), so a DB reset without it leaks the previous
    ///     test's world into this one — same technique as DevSyncService after its raw-SQL
    ///     populate. The stub restore keeps a test that swapped in failure stubs from
    ///     poisoning the next one.
    /// </summary>
    public async Task ResetDatabaseAsync()
    {
        await using (var connection = new SqlConnection(_sqlContainer.GetConnectionString()))
        {
            await connection.OpenAsync();
            await _respawner!.ResetAsync(connection);
        }

        if (_factory!.Services.GetRequiredService<IMemoryCache>() is MemoryCache concrete) concrete.Clear();

        PiuGame.Reset();
        PiuGame.MapPiuGameSite();
    }

    /// <summary>A fresh isolated browser context (own cookies/session) pointed at nothing yet.</summary>
    public Task<IBrowserContext> NewBrowserContextAsync()
    {
        return Browser.NewContextAsync(new BrowserNewContextOptions { BaseURL = BaseUrl });
    }

    public async Task DisposeAsync()
    {
        if (Browser is not null) await Browser.DisposeAsync();
        _playwright?.Dispose();
        if (_factory is not null) await _factory.DisposeAsync();
        PiuGame?.Stop();
        await _sqlContainer.DisposeAsync();
    }

    private sealed class TestDbContextFactory(DbContextOptions<ChartAttemptDbContext> options)
        : IDbContextFactory<ChartAttemptDbContext>
    {
        public ChartAttemptDbContext CreateDbContext()
        {
            return new ChartAttemptDbContext(options, VerticalModelContributions.All());
        }
    }
}
