using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ScoreTracker.Data.Configuration;
using ScoreTracker.Data.Persistence.Entities;
using ScoreTracker.Domain.Enums;

namespace ScoreTracker.Data.Persistence;

public sealed class ChartAttemptDbContext : DbContext
{
    private readonly SqlConfiguration _configuration;

#pragma warning disable CS8618
    public ChartAttemptDbContext(DbContextOptions<ChartAttemptDbContext> options, IOptions<SqlConfiguration> sqlOptions)
#pragma warning restore CS8618
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
    public DbSet<PhoenixRecordEntity> PhoenixBestAttempt { get; set; }
    public DbSet<UserSettingsEntity> UserSettings { get; set; }
    public DbSet<UserOfficialLeaderboardEntity> UserOfficialLeaderboard { get; set; }
    public DbSet<UserCoOpRatingEntity> UserCoOpRating { get; set; }
    public DbSet<CoOpRatingEntity> CoOpRating { get; set; }
    public DbSet<TournamentEntity> Tournament { get; set; }
    public DbSet<UserTournamentSessionEntity> UserTournamentSession { get; set; }
    public DbSet<PhotoVerificationEntity> PhotoVerification { get; set; }
    public DbSet<UserQualifierEntity> UserQualifier { get; set; }
    public DbSet<UserQualifierHistoryEntity> UserQualifierHistory { get; set; }
    public DbSet<UserPreferenceRatingEntity> UserPreferenceRating { get; set; }
    public DbSet<ChartPreferenceRatingEntity> ChartPreferenceRating { get; set; }
    public DbSet<UserApiTokenEntity> UserApiToken { get; set; }
    public DbSet<TierListEntryEntity> TierListEntry { get; set; }
    public DbSet<MatchEntity> Match { get; set; }
    public DbSet<RandomSettingsEntity> RandomSettings { get; set; }
    public DbSet<MatchLinkEntity> MatchLink { get; set; }
    public DbSet<UserWorldRanking> UserWorldRanking { get; set; }
    public DbSet<ChartSkillEntity> ChartSkill { get; set; }
    public DbSet<PlayerStatsEntity> PlayerStats { get; set; }
    public DbSet<BountyLeaaderboardEntity> BountyLeaderboard { get; set; }
    public DbSet<CommunityEntity> Community { get; set; }
    public DbSet<TournamentChartLevelEntity> TournamentChartLevel { get; set; }
    public DbSet<PlayerHistoryEntity> PlayerHistory { get; set; }
    public DbSet<CommunityChannelEntity> CommunityChannel { get; set; }
    public DbSet<ChartBountyEntity> ChartBounty { get; set; }
    public DbSet<CommunityInviteCodeEntity> CommunityInviteCode { get; set; }
    public DbSet<CommunityMembershipEntity> CommunityMembership { get; set; }
    public DbSet<SuggestionFeedbackEntity> SuggestionFeedback { get; set; }
    public DbSet<UserTitleEntity> UserTitle { get; set; }
    public DbSet<OfficialUserAvatarEntity> OfficialUserAvatar { get; set; }
    public DbSet<QualifiersConfigurationEntity> QualifiersConfiguration { get; set; }
    public DbSet<UserHighestTitleEntity> UserHighestTitle { get; set; }
    public DbSet<UserRandomSettingsEntity> UserRandomSettings { get; set; }
    public DbSet<TournamentRoleEntity> TournamentRole { get; set; }
    public DbSet<TournamentPlayerEntity> TournamentPlayer { get; set; }
    public DbSet<SongNameLanguageEntity> SongNameLanguage { get; set; }
    public DbSet<WeeklyTournamentChartEntity> WeeklyTournamentChart { get; set; }
    public DbSet<WeeklyUserEntry> WeeklyUserEntry { get; set; }
    public DbSet<UserWeeklyPlacingEntity> UserWeeklyPlacing { get; set; }
    public DbSet<PastTourneyChartsEntity> PastTourneyCharts { get; set; }
    public DbSet<CoOpTeamEntity> CoOpTeam { get; set; }
    public DbSet<CoOpPlayerEntity> CoOpPlayers { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.HasDefaultSchema("scores");

        builder.Entity<CoOpRatingEntity>().ToTable("CoOpRating")
            .HasOne<ChartEntity>()
            .WithMany()
            .HasForeignKey(c => c.ChartId);
        builder.Entity<UserCoOpRatingEntity>().ToTable("UserCoOpRating")
            .HasOne<ChartEntity>()
            .WithMany()
            .HasForeignKey(c => c.ChartId);
        builder.Entity<UserCoOpRatingEntity>()
            .HasOne<UserEntity>()
            .WithMany()
            .HasForeignKey(c => c.UserId);
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

        builder.Entity<PhoenixRecordEntity>().ToTable("PhoenixRecord")
            .HasOne<ChartEntity>()
            .WithMany()
            .HasForeignKey(ba => ba.ChartId);

        builder.Entity<PhoenixRecordEntity>()
            .HasOne<UserEntity>()
            .WithMany()
            .HasForeignKey(ba => ba.UserId);
        builder.Entity<UserEntity>()
            .Property(u => u.ProfileImage)
            .HasDefaultValue("https://piuimages.arroweclip.se/avatars/4f617606e7751b2dc2559d80f09c40bf.png");

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

        builder.Entity<ChartDifficultyRatingEntity>().ToTable("ChartDifficultyRating")
            .HasKey(cdr => new { cdr.ChartId, cdr.MixId });

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

        builder.Entity<UserTitleEntity>().Property(e => e.ParagonLevel)
            .HasDefaultValue(ParagonLevel.None.ToString());
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

        builder.Entity<ChartVideoEntity>().ToTable("ChartVideo")
            .HasOne<ChartEntity>()
            .WithMany()
            .HasForeignKey(e => e.ChartId);

        builder.Entity<UserTournamentSessionEntity>()
            .Property(u => u.RestTime)
            .HasDefaultValue(TimeSpan.Zero);
        builder.Entity<UserTournamentSessionEntity>()
            .Property(u => u.ChartsPlayed)
            .HasDefaultValue(0);
        builder.Entity<UserTournamentSessionEntity>()
            .Property(u => u.AverageDifficulty)
            .HasDefaultValue(1);

        builder.Entity<UserTournamentSessionEntity>()
            .Property(u => u.NeedsApproval)
            .HasDefaultValue(true);

        builder.Entity<UserTournamentSessionEntity>()
            .Property(u => u.VerificationType)
            .HasDefaultValue(SubmissionVerificationType.Unverified.ToString());

        builder.Entity<UserQualifierEntity>()
            .Property(e => e.TournamentId)
            .HasDefaultValue(new Guid("fa27b7fb-6ef4-481b-8eee-56fdcf58433c"));

        builder.Entity<MatchEntity>()
            .Property(e => e.TournamentId)
            .HasDefaultValue(new Guid("fa27b7fb-6ef4-481b-8eee-56fdcf58433c"));
        builder.Entity<MatchLinkEntity>()
            .Property(e => e.TournamentId)
            .HasDefaultValue(new Guid("fa27b7fb-6ef4-481b-8eee-56fdcf58433c"));
        builder.Entity<RandomSettingsEntity>()
            .Property(e => e.TournamentId)
            .HasDefaultValue(new Guid("fa27b7fb-6ef4-481b-8eee-56fdcf58433c"));
        builder.Entity<TournamentEntity>()
            .Property(e => e.Type)
            .HasDefaultValue(nameof(TournamentType.Stamina));

        builder.Entity<TournamentEntity>()
            .Property(e => e.Location)
            .HasDefaultValue("Remote");

        builder.Entity<QualifiersConfigurationEntity>()
            .Property(e => e.ChartPlayCount)
            .HasDefaultValue(3);
    }
}