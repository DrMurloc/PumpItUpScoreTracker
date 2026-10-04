using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Respawn;
using ScoreTracker.CompositionRoot;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Data.Persistence.Entities;
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

    /// <summary>
    ///     The <c>scores.Mix</c> rows exactly as the migrations seeded them. Read straight after
    ///     migrating, because the first <see cref="ResetAsync" /> clears that table along with every
    ///     other one in the schema.
    /// </summary>
    public IReadOnlyList<MixEntity> MigratedMixes { get; private set; } = Array.Empty<MixEntity>();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var options = new DbContextOptionsBuilder<ChartAttemptDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;
        _factory = new TestDbContextFactory(options);

        await using var context = await _factory.CreateDbContextAsync();
        await context.Database.MigrateAsync();
        MigratedMixes = await context.Mix.AsNoTracking().ToListAsync();

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

    /// <summary>
    ///     A second database on the same server, migrated from nothing and never reset: the state a
    ///     local run leaves a developer's database in before anything else writes to it. It costs a
    ///     second full migration, so only a test that needs the seeded reference data intact asks.
    /// </summary>
    public async Task<IDbContextFactory<ChartAttemptDbContext>> CreateMigratedDatabaseAsync()
    {
        var connectionString = new SqlConnectionStringBuilder(ConnectionString)
        {
            InitialCatalog = $"Migrated_{Guid.NewGuid():N}"
        }.ConnectionString;
        var options = new DbContextOptionsBuilder<ChartAttemptDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        IDbContextFactory<ChartAttemptDbContext> factory = new TestDbContextFactory(options);

        await using var context = await factory.CreateDbContextAsync();
        await context.Database.MigrateAsync();
        return factory;
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();

    private sealed class TestDbContextFactory(DbContextOptions<ChartAttemptDbContext> options)
        : IDbContextFactory<ChartAttemptDbContext>
    {
        public ChartAttemptDbContext CreateDbContext() => new(options, VerticalModelContributions.All());
    }
}
