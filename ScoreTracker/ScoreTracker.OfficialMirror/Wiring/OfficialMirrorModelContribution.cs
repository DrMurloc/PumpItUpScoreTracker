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
        modelBuilder.Entity<UserOfficialLeaderboardEntity>().ToTable("UserOfficialLeaderboard");
        modelBuilder.Entity<UserWorldRanking>().ToTable("UserWorldRanking");
        modelBuilder.Entity<OfficialUserAvatarEntity>().ToTable("OfficialUserAvatar");
        modelBuilder.Entity<OfficialLeaderboardImportStateEntity>().ToTable("OfficialLeaderboardImportState");

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
        placement.HasIndex(e => new { e.PlayerId, e.SnapshotId })
            .IncludeProperties(e => new { e.LeaderboardId, e.Place, e.Score });
        placement.Property(e => e.Score).HasPrecision(9, 2);

        modelBuilder.Entity<OfficialChartPopularityEntity>().ToTable("OfficialChartPopularity")
            .HasKey(e => new { e.SnapshotId, e.ChartId });

        modelBuilder.Entity<OfficialBoardRecordEntity>().ToTable("OfficialBoardRecord");

        modelBuilder.Entity<OfficialFolderRecordEntity>().ToTable("OfficialFolderRecord")
            .HasKey(e => new { e.MixId, e.ChartType, e.Level });

        var highlight = modelBuilder.Entity<OfficialWeeklyHighlightEntity>().ToTable("OfficialWeeklyHighlight");
        highlight.HasIndex(e => e.SnapshotId);
        highlight.Property(e => e.Score).HasPrecision(9, 2);
        highlight.Property(e => e.PrevValue).HasPrecision(9, 2);
        highlight.Property(e => e.NewValue).HasPrecision(9, 2);

        modelBuilder.Entity<OfficialPlayerRenameProposalEntity>().ToTable("OfficialPlayerRenameProposal")
            .HasIndex(e => new { e.MixId, e.Status });

        // The unique index rides the entity attribute; table name pinned here like the rest.
        modelBuilder.Entity<OfficialMissingChartEntity>().ToTable("OfficialMissingChart");
    }
}
