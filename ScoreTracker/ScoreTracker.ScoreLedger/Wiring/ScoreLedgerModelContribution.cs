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

        // Covers the cohort ranking reads (mix + chart-set lookups projecting
        // user/score/plate); without it they scan the whole table. Built ONLINE because
        // the migration bundle applies against the live table during deploys.
        modelBuilder.Entity<PhoenixRecordEntity>()
            .HasIndex(e => new { e.MixId, e.ChartId })
            .IncludeProperties(e => new { e.UserId, e.Score, e.Plate, e.IsBroken })
            .IsCreatedOnline();

        modelBuilder.Entity<ScoreEventJournalEntity>().ToTable("ScoreEventJournal");
        // Session lookups skip the pre-capture rows (SessionId is never backfilled).
        modelBuilder.Entity<ScoreEventJournalEntity>().HasIndex(e => e.SessionId)
            .HasFilter("[SessionId] IS NOT NULL");
        modelBuilder.Entity<PhoenixRecordStatsEntity>().ToTable("PhoenixRecordStats");

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
