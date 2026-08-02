using Microsoft.EntityFrameworkCore;
using ScoreTracker.CommunityTools.Infrastructure.Entities;
using ScoreTracker.Data.Persistence;

namespace ScoreTracker.CommunityTools.Wiring;

/// <summary>
///     Registers the Community Tools entities with the single <see cref="ChartAttemptDbContext" />
///     (ADR-001 D4).
/// </summary>
public sealed class CommunityToolsModelContribution : IDbModelContribution
{
    public void Contribute(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ToolEntity>().ToTable("Tool");
        modelBuilder.Entity<ToolMixSubscriptionEntity>().ToTable("ToolMixSubscription");
        modelBuilder.Entity<ToolInviteCodeEntity>().ToTable("ToolInviteCode");
        modelBuilder.Entity<ToolShareEntity>().ToTable("ToolShare");
        modelBuilder.Entity<ToolBlockEntity>().ToTable("ToolBlock");
        modelBuilder.Entity<ToolSharePreferenceEntity>().ToTable("ToolSharePreference");
        modelBuilder.Entity<ToolApiKeyEntity>().ToTable("ToolApiKey");
        modelBuilder.Entity<WebhookDeliveryEntity>().ToTable("WebhookDelivery");
        modelBuilder.Entity<ToolActivityEntity>().ToTable("ToolActivity");

        // The lookups that run on every authenticated api/v2 call and every import fan-out.
        modelBuilder.Entity<ToolApiKeyEntity>().HasIndex(k => k.KeyHash).IsUnique();
        modelBuilder.Entity<ToolShareEntity>().HasIndex(s => new { s.ToolId, s.UserId });
        modelBuilder.Entity<ToolShareEntity>().HasIndex(s => s.UserId);
        modelBuilder.Entity<ToolBlockEntity>().HasIndex(b => new { b.ToolId, b.UserId }).IsUnique();
        modelBuilder.Entity<ToolMixSubscriptionEntity>().HasIndex(m => new { m.ToolId, m.MixId }).IsUnique();
        modelBuilder.Entity<WebhookDeliveryEntity>().HasIndex(d => new { d.ToolId, d.QueuedAt });
        // The retry sweep's query: everything still owed a delivery, oldest first.
        modelBuilder.Entity<WebhookDeliveryEntity>().HasIndex(d => d.NextAttemptAt);
        modelBuilder.Entity<ToolActivityEntity>().HasIndex(a => new { a.ToolId, a.OccurredAt });
    }
}
