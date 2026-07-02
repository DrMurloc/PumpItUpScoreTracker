using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;
using ScoreTracker.OfficialMirror.Infrastructure.Entities;

namespace ScoreTracker.OfficialMirror.Wiring;

/// <summary>
///     Registers the Official Mirror's entities with the single <see cref="ChartAttemptDbContext" />
///     (ADR-001 D4). Table names are pinned explicitly because they used to come from the
///     context's deleted DbSet property names; all four entities are attribute-configured.
/// </summary>
public sealed class OfficialMirrorModelContribution : IDbModelContribution
{
    public void Contribute(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserOfficialLeaderboardEntity>().ToTable("UserOfficialLeaderboard");
        modelBuilder.Entity<UserWorldRanking>().ToTable("UserWorldRanking");
        modelBuilder.Entity<OfficialUserAvatarEntity>().ToTable("OfficialUserAvatar");
        modelBuilder.Entity<OfficialLeaderboardImportStateEntity>().ToTable("OfficialLeaderboardImportState");
    }
}
