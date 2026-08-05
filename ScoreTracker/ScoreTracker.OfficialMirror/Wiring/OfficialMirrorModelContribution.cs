using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;
using ScoreTracker.OfficialMirror.Infrastructure.Entities;

namespace ScoreTracker.OfficialMirror.Wiring;

/// <summary>
///     Registers the Official Mirror's entities with the single <see cref="ChartAttemptDbContext" />
///     (ADR-001 D4). The four legacy tables persist until the post-deploy baseline seed is
///     verified in production; the snapshot-model tables are the system of record.
/// </summary>
public sealed class OfficialMirrorModelContribution : IDbModelContribution
{
    public void Contribute(ModelBuilder modelBuilder)
    {

        var leaderboard = modelBuilder.Entity<OfficialLeaderboardEntity>().ToTable("OfficialLeaderboard");
        leaderboard.HasIndex(e => new { e.MixId, e.LeaderboardType, e.Name }).IsUnique();
        leaderboard.HasIndex(e => e.ChartId);

        var player = modelBuilder.Entity<OfficialPlayerEntity>().ToTable("OfficialPlayer");
        player.HasIndex(e => new { e.MixId, e.Username }).IsUnique();
        player.HasIndex(e => e.UserId);


        var snapshot = modelBuilder.Entity<OfficialLeaderboardSnapshotEntity>()
            .ToTable("OfficialLeaderboardSnapshot");
        snapshot.HasIndex(e => new { e.MixId, e.CompletedAt });

        var placement = modelBuilder.Entity<OfficialLeaderboardPlacementEntity>()
            .ToTable("OfficialLeaderboardPlacement");
        placement.HasKey(e => new { e.SnapshotId, e.LeaderboardId, e.Place, e.PlayerId });
        // IsSupplemented rides the INCLUDE rather than the key: a board read is already a
        // clustered range of a few hundred rows, so the flag costs a residual predicate
        // there, but a player timeline seeks this index and would otherwise pay a lookup
        // per row just to learn which reading each placement belongs to.
        placement.HasIndex(e => new { e.PlayerId, e.SnapshotId })
            .IncludeProperties(e => new { e.LeaderboardId, e.Place, e.Score, e.IsSupplemented });
        placement.Property(e => e.Score).HasPrecision(9, 2);

        modelBuilder.Entity<OfficialChartPopularityEntity>().ToTable("OfficialChartPopularity")
            .HasKey(e => new { e.SnapshotId, e.ChartId });

        modelBuilder.Entity<OfficialBoardRecordEntity>().ToTable("OfficialBoardRecord");

        modelBuilder.Entity<OfficialFolderRecordEntity>().ToTable("OfficialFolderRecord")
            .HasKey(e => new { e.MixId, e.ChartType, e.Level });

        var highlight = modelBuilder.Entity<OfficialWeeklyHighlightEntity>().ToTable("OfficialWeeklyHighlight");
        // Highlights are always read as "this snapshot, this reading", so the flag belongs in
        // the key rather than the INCLUDE — unlike placements, where a board range is small
        // enough that a residual predicate is cheaper than a wider index.
        highlight.HasIndex(e => new { e.SnapshotId, e.IsSupplemented });
        highlight.Property(e => e.Score).HasPrecision(9, 2);
        highlight.Property(e => e.PrevValue).HasPrecision(9, 2);
        highlight.Property(e => e.NewValue).HasPrecision(9, 2);

        modelBuilder.Entity<OfficialPlayerRenameProposalEntity>().ToTable("OfficialPlayerRenameProposal")
            .HasIndex(e => new { e.MixId, e.Status });

        // The unique index rides the entity attribute; table name pinned here like the rest.
        modelBuilder.Entity<OfficialMissingChartEntity>().ToTable("OfficialMissingChart");
    }
}
