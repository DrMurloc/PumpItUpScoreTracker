using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Respawn;
using ScoreTracker.CompositionRoot;
using ScoreTracker.Data.Persistence;
using Testcontainers.MsSql;

namespace ScoreTracker.Tests.Integration.Fixtures;

[ExcludeFromCodeCoverage]
public sealed class SqlServerFixture : IAsyncLifetime
{
    // Production runs on Azure SQL Database; SQL Server 2025 is the closest local equivalent
    // (compat level + T-SQL surface). Pin the image explicitly rather than relying on whatever
    // version Testcontainers.MsSql defaults to.
    private readonly MsSqlContainer _container = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2025-latest")
        .Build();
    private IDbContextFactory<ChartAttemptDbContext>? _factory;
    private Respawner? _respawner;

    public string ConnectionString => _container.GetConnectionString();

    public IDbContextFactory<ChartAttemptDbContext> DbContextFactory =>
        _factory ?? throw new InvalidOperationException("Fixture not initialized. Call InitializeAsync first.");

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var options = new DbContextOptionsBuilder<ChartAttemptDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;
        _factory = new TestDbContextFactory(options);

        await using var context = await _factory.CreateDbContextAsync();
        await context.Database.MigrateAsync();

        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.SqlServer,
            SchemasToInclude = new[] { "scores" }
        });
    }

    public async Task ResetAsync()
    {
        if (_respawner is null) return;
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        await _respawner.ResetAsync(connection);
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();

    private sealed class TestDbContextFactory(DbContextOptions<ChartAttemptDbContext> options)
        : IDbContextFactory<ChartAttemptDbContext>
    {
        public ChartAttemptDbContext CreateDbContext() => new(options, VerticalModelContributions.All());
    }
}
