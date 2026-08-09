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

        modelBuilder.Entity<ScoreSessionEntity>().ToTable("ScoreSession");

        modelBuilder.Entity<ScoreEventJournalEntity>().ToTable("ScoreEventJournal");
        // Session lookups skip the pre-capture rows (SessionId is never backfilled).
        modelBuilder.Entity<ScoreEventJournalEntity>().HasIndex(e => e.SessionId)
            .HasFilter("[SessionId] IS NOT NULL");
        // Covers the limbo board's one read: every player's plays on ONE chart. The three
        // indexes above all lead with UserId, so without this it scans the whole journal.
        // Source is deliberately not included — nothing filters on it (limbo-leaderboard D6)
        // and nvarchar(32) costs a third of the index for nothing. OccurredAt is, because the
        // board's tie order is by date. Built ONLINE: the bundle applies against the live table.
        modelBuilder.Entity<ScoreEventJournalEntity>()
            .HasIndex(e => new { e.ChartId, e.MixId })
            .IncludeProperties(e => new { e.UserId, e.Score, e.IsBroken, e.OccurredAt })
            .IsCreatedOnline();

        // Presence-only: the charts carrying a limbo leaderboard, inserted by hand
        // (docs/design/limbo-leaderboard.md D1). No FK onto Chart — a flag naming a chart that
        // does not exist yet is inert rather than a failed INSERT, which is the friendlier
        // shape for a table whose only writer is a person at a SQL prompt.
        modelBuilder.Entity<LimboChartEntity>().ToTable("LimboChart")
            .HasKey(e => new { e.MixId, e.ChartId });
        modelBuilder.Entity<PhoenixRecordStatsEntity>().ToTable("PhoenixRecordStats");

        modelBuilder.Entity<BestAttemptEntity>().ToTable("BestAttempt")
            .HasOne<ChartEntity>()
            .WithMany()
            .HasForeignKey(ba => ba.ChartId);

        modelBuilder.Entity<BestAttemptEntity>()
            .HasOne<UserEntity>()
            .WithMany()
            .HasForeignKey(ba => ba.UserId);

        // XX is the table's original, implicit scope — the column default keeps every
        // pre-legacy-mix row valid without a data backfill (PhoenixRecordsPerMix precedent).
        modelBuilder.Entity<BestAttemptEntity>()
            .Property(ba => ba.MixId)
            .HasDefaultValue(MixIds.XX);
    }
}
