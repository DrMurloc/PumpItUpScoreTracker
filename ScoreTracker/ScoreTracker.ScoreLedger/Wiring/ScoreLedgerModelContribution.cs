using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Data.Persistence.Entities;
using ScoreTracker.ScoreLedger.Infrastructure.Entities;

namespace ScoreTracker.ScoreLedger.Wiring;

/// <summary>
///     Registers the Score Ledger's entities with the single <see cref="ChartAttemptDbContext" />
///     (ADR-001 D4). The PhoenixRecord table mapping and its FK relations are reproduced
///     verbatim from the context's former OnModelCreating block; ScoreEventJournal's table
///     name is pinned because it used to come from the deleted DbSet property name.
/// </summary>
public sealed class ScoreLedgerModelContribution : IDbModelContribution
{
    public void Contribute(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PhoenixRecordEntity>().ToTable("PhoenixRecord")
            .HasOne<ChartEntity>()
            .WithMany()
            .HasForeignKey(ba => ba.ChartId);

        modelBuilder.Entity<PhoenixRecordEntity>()
            .HasOne<UserEntity>()
            .WithMany()
            .HasForeignKey(ba => ba.UserId);

        modelBuilder.Entity<ScoreEventJournalEntity>().ToTable("ScoreEventJournal");

        modelBuilder.Entity<BestAttemptEntity>().ToTable("BestAttempt")
            .HasOne<ChartEntity>()
            .WithMany()
            .HasForeignKey(ba => ba.ChartId);

        modelBuilder.Entity<BestAttemptEntity>()
            .HasOne<UserEntity>()
            .WithMany()
            .HasForeignKey(ba => ba.UserId);
    }
}
