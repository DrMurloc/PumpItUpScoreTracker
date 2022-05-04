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
    public DbSet<UserEntity> User { get; set; }
    public DbSet<DiscordLoginEntity> DiscordLogin { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.HasDefaultSchema("scores");

        builder.Entity<BestAttemptEntity>().ToTable("BestAttempt")
            .HasOne<ChartEntity>()
            .WithMany()
            .HasForeignKey(ba => ba.ChartId);

        builder.Entity<BestAttemptEntity>()
            .HasOne<UserEntity>()
            .WithMany()
            .HasForeignKey(ba => ba.UserId);

        builder.Entity<ChartEntity>().ToTable("Chart")
            .HasOne<SongEntity>()
            .WithMany()
            .HasForeignKey(c => c.SongId);

        builder.Entity<SongEntity>().ToTable("Song");

        builder.Entity<UserEntity>().ToTable("User");

        builder.Entity<DiscordLoginEntity>().ToTable("DiscordLogin")
            .HasOne<UserEntity>()
            .WithMany()
            .HasForeignKey(d => d.UserId);

        builder.Entity<ExternalLoginEntity>().ToTable("ExternalLogin")
            .HasKey(e => new { e.LoginProvider, e.ExternalId });

        builder.Entity<ExternalLoginEntity>()
            .HasOne<UserEntity>()
            .WithMany()
            .HasForeignKey(e => e.UserId);
    }
}