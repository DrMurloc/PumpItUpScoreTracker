using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence.Entities;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Data.Persistence;

public sealed class ChartAttemptDbContext : DbContext
{
    private readonly IEnumerable<IDbModelContribution> _contributions;

#pragma warning disable CS8618
    public ChartAttemptDbContext(DbContextOptions<ChartAttemptDbContext> options,
        IEnumerable<IDbModelContribution>? contributions = null)
#pragma warning restore CS8618
        : base(options)
    {
        _contributions = contributions ?? Array.Empty<IDbModelContribution>();
    }

    public DbSet<MixEntity> Mix { get; set; }
    public DbSet<ChartMixEntity> ChartMix { get; set; }
    public DbSet<ChartEntity> Chart { get; set; }
    public DbSet<SongEntity> Song { get; set; }
    public DbSet<UserEntity> User { get; set; }
    public DbSet<ExternalLoginEntity> ExternalLogin { get; set; }
    public DbSet<SavedChartEntity> SavedChart { get; set; }
    public DbSet<UserSettingsEntity> UserSettings { get; set; }
    public DbSet<UserApiTokenEntity> UserApiToken { get; set; }
    public DbSet<MatchEntity> Match { get; set; }
    public DbSet<RandomSettingsEntity> RandomSettings { get; set; }
    public DbSet<MatchLinkEntity> MatchLink { get; set; }
    public DbSet<TournamentPlayerEntity> TournamentPlayer { get; set; }
    public DbSet<TournamentMachineEntity> TournamentMachine { get; set; }
    public DbSet<CountryEntity> Country { get; set; }
    public DbSet<ChartLetterDifficultyEntity> ChartLetterDifficulty { get; set; }

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

        builder.Entity<UserEntity>()
            .Property(u => u.ProfileImage)
            .HasDefaultValue("https://piuimages.arroweclip.se/avatars/4f617606e7751b2dc2559d80f09c40bf.png");



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
        builder.Entity<SongEntity>()
            .Property(s => s.Type)
            .HasDefaultValue("Arcade");

        builder.Entity<SongEntity>().Property(s => s.Duration)
            .HasDefaultValue(TimeSpan.FromMinutes(0))
            .HasConversion<long>();
        builder.Entity<UserEntity>().ToTable("User");

        builder.Entity<ExternalLoginEntity>().ToTable("ExternalLogin")
            .HasKey(e => new { e.LoginProvider, e.ExternalId });

        builder.Entity<ExternalLoginEntity>()
            .HasOne<UserEntity>()
            .WithMany()
            .HasForeignKey(e => e.UserId);

        builder.Entity<MatchEntity>()
            .Property(e => e.TournamentId)
            .HasDefaultValue(new Guid("fa27b7fb-6ef4-481b-8eee-56fdcf58433c"));
        builder.Entity<MatchLinkEntity>()
            .Property(e => e.TournamentId)
            .HasDefaultValue(new Guid("fa27b7fb-6ef4-481b-8eee-56fdcf58433c"));
        builder.Entity<RandomSettingsEntity>()
            .Property(e => e.TournamentId)
            .HasDefaultValue(new Guid("fa27b7fb-6ef4-481b-8eee-56fdcf58433c"));

        builder.Entity<ChartEntity>()
            .Property(e => e.OriginalMixId)
            .HasDefaultValue(new Guid("1ABB8F5A-BDA3-40F0-9CE7-1C4F9F8F1D3B"));
        builder.Entity<ChartEntity>()
            .Property(e => e.PlayerCount)
            .HasDefaultValue(1);

        // Vertical-owned entities (ADR-001 D4). Applied last so contributions see the
        // default schema and shared conventions.
        foreach (var contribution in _contributions) contribution.Contribute(builder);
    }
}
