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

        // The season leads every index it is part of (docs/design/seasons.md D34): the all-time rows
        // stay one dense range that seasonal inserts never split, and each season appends as its own
        // cold range. Named, because two unnamed indexes over one column set collapse into one in the
        // model (see the journal indexes below). Built ONLINE: the migration bundle applies against
        // the live table during deploys.
        //
        // The unique key keeps the player first: EF's foreign-key convention would otherwise add a
        // fourth index on UserId, and the per-player read seeks user then season regardless.
        modelBuilder.Entity<PhoenixRecordEntity>()
            .HasIndex(e => new { e.UserId, e.SeasonId, e.ChartId, e.MixId },
                "IX_PhoenixRecord_UserId_SeasonId_ChartId_MixId")
            .IsUnique()
            .IsCreatedOnline();
        // Covers the cohort ranking reads (mix + chart-set lookups projecting user/score/plate);
        // without it they scan the whole table.
        modelBuilder.Entity<PhoenixRecordEntity>()
            .HasIndex(e => new { e.SeasonId, e.MixId, e.ChartId }, "IX_PhoenixRecord_SeasonId_MixId_ChartId")
            .IncludeProperties(e => new { e.UserId, e.Score, e.Plate, e.IsBroken })
            .IsCreatedOnline();
        // The chart foreign key's own index, unchanged: a read by chart alone filters the season
        // during the key lookups it already makes.
        modelBuilder.Entity<PhoenixRecordEntity>().HasIndex(e => e.ChartId);
        // Every read sees all-time bests unless it drops this filter by name (docs/design/seasons.md
        // D12): a seasonal reader drops AllTime and applies its own season, UserDataPurge drops every
        // filter, and PeerScoreStore's raw SQL spells SeasonId = 0 itself.
        modelBuilder.Entity<PhoenixRecordEntity>().HasQueryFilter(QueryFilters.AllTime, e => e.SeasonId == 0);

        modelBuilder.Entity<ScoreSessionEntity>().ToTable("ScoreSession");
        // The restart-recovery pass asks only "which sessions never finished their derived work".
        // Filtered, so the index holds the handful of in-flight and interrupted sessions rather
        // than a row per session ever recorded (docs/design/import-restart-recovery.md §5.3).
        modelBuilder.Entity<ScoreSessionEntity>().HasIndex(e => e.ProcessedAt)
            .HasFilter("[ProcessedAt] IS NULL");

        modelBuilder.Entity<ScoreEventJournalEntity>().ToTable("ScoreEventJournal");
        // F, D, C, B, A, S, SS, SSS — three characters is the longest there is.
        modelBuilder.Entity<ScoreEventJournalEntity>().Property(e => e.LetterGrade).HasMaxLength(4);
        // Session lookups skip the pre-capture rows (SessionId is never backfilled).
        modelBuilder.Entity<ScoreEventJournalEntity>().HasIndex(e => e.SessionId)
            .HasFilter("[SessionId] IS NOT NULL");
        // Covers the limbo board's one read: every player's plays on ONE chart. The three
        // indexes above all lead with UserId, so without this it scans the whole journal.
        // Source is deliberately not included — nothing filters on it (limbo-leaderboard D6)
        // and nvarchar(32) costs a third of the index for nothing. OccurredAt is, because the
        // board's tie order is by date. Built ONLINE: the bundle applies against the live table.
        // Named in the HasIndex call, not via HasDatabaseName: the failure-rail index below
        // shares these key columns, and two unnamed HasIndex calls on one column set collapse
        // into ONE model index — scaffolding then DROPS this one to make room, which is how a
        // migration nearly deleted the limbo board's covering index out from under it.
        modelBuilder.Entity<ScoreEventJournalEntity>()
            .HasIndex(e => new { e.ChartId, e.MixId }, "IX_ScoreEventJournal_ChartId_MixId")
            .IncludeProperties(e => new { e.UserId, e.Score, e.IsBroken, e.OccurredAt })
            .IsCreatedOnline();
        // The failure rail's read: one chart's judged stage breaks, every player
        // (docs/design/step-chart-failure-map.md §3). Filtered to breaks so it holds a fraction
        // of a percent of the journal. Built ONLINE like its neighbours.
        modelBuilder.Entity<ScoreEventJournalEntity>()
            .HasIndex(e => new { e.ChartId, e.MixId }, "IX_ScoreEventJournal_ChartId_MixId_StageBreaks")
            .HasFilter("[IsStageBroken] = 1")
            .IncludeProperties(e => new
                { e.UserId, e.Perfects, e.Greats, e.Goods, e.Bads, e.Misses, e.IsNonLifebarBreak })
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
