using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence.Entities;

namespace ScoreTracker.Data.Persistence;

public sealed class ChartAttemptDbContext : DbContext
{
    public ChartAttemptDbContext(DbContextOptions<ChartAttemptDbContext> options) : base(options)
    {
    }

    public DbSet<BestAttemptEntity> BestAttempt { get; set; }
    public DbSet<ChartEntity> Chart { get; set; }
    public DbSet<SongEntity> Song { get; set; }
}