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
    }
}
