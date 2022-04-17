using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ScoreTracker.Data.Configuration;
using ScoreTracker.Data.Persistence.Entities;

namespace ScoreTracker.Data.Persistence;

public sealed class ChartAttemptDbContext : DbContext
{
    private readonly SqlConfiguration _configuration;

    public ChartAttemptDbContext(DbContextOptions<ChartAttemptDbContext> options, IOptions<SqlConfiguration> sqlOptions)
        : base(options)
    {
        _configuration = sqlOptions.Value;
    }

    public DbSet<BestAttemptEntity> BestAttempt { get; set; }
    public DbSet<ChartEntity> Chart { get; set; }
    public DbSet<SongEntity> Song { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.HasDefaultSchema("scores");

        builder.Entity<BestAttemptEntity>().ToTable("BestAttempt");

        builder.Entity<ChartEntity>().ToTable("Chart");

        builder.Entity<SongEntity>().ToTable("Song");
    }
}