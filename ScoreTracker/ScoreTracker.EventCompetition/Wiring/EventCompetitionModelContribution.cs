using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.EventCompetition.Infrastructure.Entities;

namespace ScoreTracker.EventCompetition.Wiring;

/// <summary>
///     Registers the Event Competition entities with the single <see cref="ChartAttemptDbContext" />
///     (ADR-001 D4). Table names are pinned because they used to come from the context's
///     deleted DbSet property names; the default-value blocks moved verbatim from the
///     context's OnModelCreating. The Match subsystem's entities (Match, MatchLink,
///     RandomSettings, TournamentPlayer, TournamentMachine) stay in ScoreTracker.Data —
///     they are C5-gated for deletion, not part of this vertical.
/// </summary>
public sealed class EventCompetitionModelContribution : IDbModelContribution
{
    public void Contribute(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TournamentEntity>().ToTable("Tournament");
        modelBuilder.Entity<UserTournamentSessionEntity>().ToTable("UserTournamentSession");
        modelBuilder.Entity<PhotoVerificationEntity>().ToTable("PhotoVerification");
        modelBuilder.Entity<TournamentChartLevelEntity>().ToTable("TournamentChartLevel");
        modelBuilder.Entity<TournamentRoleEntity>().ToTable("TournamentRole");
        modelBuilder.Entity<TournamentRoleInviteEntity>().ToTable("TournamentRoleInvite");
        modelBuilder.Entity<UserQualifierEntity>().ToTable("UserQualifier");
        modelBuilder.Entity<UserQualifierHistoryEntity>().ToTable("UserQualifierHistory");
        modelBuilder.Entity<QualifiersConfigurationEntity>().ToTable("QualifiersConfiguration");
        modelBuilder.Entity<CoOpTeamEntity>().ToTable("CoOpTeam");
        modelBuilder.Entity<CoOpPlayerEntity>().ToTable("CoOpPlayers");
        modelBuilder.Entity<UserTournamentRegistrationEntity>().ToTable("UserTournamentRegistration");

        modelBuilder.Entity<UserTournamentSessionEntity>()
            .Property(u => u.RestTime)
            .HasDefaultValue(TimeSpan.Zero);
        modelBuilder.Entity<UserTournamentSessionEntity>()
            .Property(u => u.ChartsPlayed)
            .HasDefaultValue(0);
        modelBuilder.Entity<UserTournamentSessionEntity>()
            .Property(u => u.AverageDifficulty)
            .HasDefaultValue(1);
        modelBuilder.Entity<UserTournamentSessionEntity>()
            .Property(u => u.NeedsApproval)
            .HasDefaultValue(true);
        modelBuilder.Entity<UserTournamentSessionEntity>()
            .Property(u => u.VerificationType)
            .HasDefaultValue(SubmissionVerificationType.Unverified.ToString());

        modelBuilder.Entity<UserQualifierEntity>()
            .Property(e => e.TournamentId)
            .HasDefaultValue(new Guid("fa27b7fb-6ef4-481b-8eee-56fdcf58433c"));

        modelBuilder.Entity<TournamentEntity>()
            .Property(e => e.Type)
            .HasDefaultValue(nameof(TournamentType.Stamina));
        modelBuilder.Entity<TournamentEntity>()
            .Property(e => e.Location)
            .HasDefaultValue("Remote");

        modelBuilder.Entity<QualifiersConfigurationEntity>()
            .Property(e => e.ChartPlayCount)
            .HasDefaultValue(3);

        // The MoM tables (docs/design/march-of-murlocs.md §6). One linear cascade chain:
        // Season → Board → Session → SessionChart, so pruning an empty season (D13) and
        // purging an account's sessions each take their children with them.
        modelBuilder.Entity<MoMSeasonEntity>().ToTable("MoMSeason");
        modelBuilder.Entity<MoMSeasonEntity>()
            .HasIndex(s => new { s.Year, s.Quarter })
            .IsUnique()
            .HasDatabaseName("UX_MoMSeason_Quarter")
            .HasFilter("[Quarter] IS NOT NULL");

        modelBuilder.Entity<MoMBoardEntity>().ToTable("MoMBoard");
        modelBuilder.Entity<MoMBoardEntity>()
            .HasIndex(b => new { b.SeasonId, b.MixId, b.ChartType })
            .IsUnique();
        modelBuilder.Entity<MoMBoardEntity>()
            .HasOne<MoMSeasonEntity>().WithMany()
            .HasForeignKey(b => b.SeasonId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<MoMChartLevelEntity>().ToTable("MoMChartLevel");
        modelBuilder.Entity<MoMChartLevelEntity>()
            .HasKey(l => new { l.SeasonId, l.MixId, l.ChartId });
        modelBuilder.Entity<MoMChartLevelEntity>()
            .HasOne<MoMSeasonEntity>().WithMany()
            .HasForeignKey(l => l.SeasonId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<MoMSessionEntity>().ToTable("MoMSession");
        modelBuilder.Entity<MoMSessionEntity>()
            .HasIndex(s => new { s.BoardId, s.TotalScore })
            .HasDatabaseName("IX_MoMSession_Board")
            .IsDescending(false, true)
            .HasFilter("[PublishedAt] IS NOT NULL");
        modelBuilder.Entity<MoMSessionEntity>()
            .HasOne<MoMBoardEntity>().WithMany()
            .HasForeignKey(s => s.BoardId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<MoMSessionChartEntity>().ToTable("MoMSessionChart");
        modelBuilder.Entity<MoMSessionChartEntity>()
            .HasKey(c => new { c.SessionId, c.Ordinal });
        modelBuilder.Entity<MoMSessionChartEntity>()
            .HasOne<MoMSessionEntity>().WithMany()
            .HasForeignKey(c => c.SessionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
