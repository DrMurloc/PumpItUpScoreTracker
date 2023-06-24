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

    public DbSet<MixEntity> Mix { get; set; }
    public DbSet<ChartMixEntity> ChartMix { get; set; }
    public DbSet<BestAttemptEntity> BestAttempt { get; set; }
    public DbSet<ChartEntity> Chart { get; set; }
    public DbSet<SongEntity> Song { get; set; }
    public DbSet<UserEntity> User { get; set; }
    public DbSet<ExternalLoginEntity> ExternalLogin { get; set; }
    public DbSet<ChartVideoEntity> ChartVideo { get; set; }
    public DbSet<SavedChartEntity> SavedChart { get; set; }
    public DbSet<UserChartDifficultyRatingEntity> UserChartDifficultyRating { get; set; }
    public DbSet<ChartDifficultyRatingEntity> ChartDifficultyRating { get; set; }
    public DbSet<UserSettingsEntity> UserSettings { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.HasDefaultSchema("scores");

        builder.Entity<ChartMixEntity>().ToTable("ChartMix")
            .HasOne<ChartEntity>()
            .WithMany()
            .HasForeignKey(cm => cm.ChartId);

        builder.Entity<ChartMixEntity>()
            .HasOne<MixEntity>()
            .WithMany()
            .HasForeignKey(cm => cm.MixId);

        builder.Entity<UserSettingsEntity>().ToTable("UserSettings")
            .HasOne<UserEntity>()
            .WithMany()
            .HasForeignKey(us => us.UserId);

        builder.Entity<BestAttemptEntity>().ToTable("BestAttempt")
            .HasOne<ChartEntity>()
            .WithMany()
            .HasForeignKey(ba => ba.ChartId);

        builder.Entity<BestAttemptEntity>()
            .HasOne<UserEntity>()
            .WithMany()
            .HasForeignKey(ba => ba.UserId);


        builder.Entity<UserChartDifficultyRatingEntity>().ToTable("UserChartDifficultyRating")
            .HasOne<UserEntity>()
            .WithMany()
            .HasForeignKey(ucdr => ucdr.UserId);

        builder.Entity<UserChartDifficultyRatingEntity>()
            .HasOne<ChartEntity>()
            .WithMany()
            .HasForeignKey(ucdr => ucdr.ChartId);

        builder.Entity<ChartDifficultyRatingEntity>().ToTable("ChartDifficultyRating");

        builder.Entity<SavedChartEntity>().ToTable("SavedChart")
            .HasOne<ChartEntity>()
            .WithMany()
            .HasForeignKey(sc => sc.ChartId);

        builder.Entity<SavedChartEntity>()
            .HasOne<UserEntity>()
            .WithMany()
            .HasForeignKey(sc => sc.UserId);

        builder.Entity<ChartEntity>().ToTable("Chart")
            .HasOne<SongEntity>()
            .WithMany()
            .HasForeignKey(c => c.SongId);

        builder.Entity<SongEntity>().ToTable("Song");

        builder.Entity<UserEntity>().ToTable("User");

        builder.Entity<ExternalLoginEntity>().ToTable("ExternalLogin")
            .HasKey(e => new { e.LoginProvider, e.ExternalId });

        builder.Entity<ExternalLoginEntity>()
            .HasOne<UserEntity>()
            .WithMany()
            .HasForeignKey(e => e.UserId);

        builder.Entity<ChartVideoEntity>().ToTable("ChartVideo")
            .HasOne<ChartEntity>()
            .WithMany()
            .HasForeignKey(e => e.ChartId);
    }
}