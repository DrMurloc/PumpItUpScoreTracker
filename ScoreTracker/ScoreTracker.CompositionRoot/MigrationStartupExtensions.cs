using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ScoreTracker.Data.Persistence;

namespace ScoreTracker.CompositionRoot;

public static class MigrationStartupExtensions
{
    /// <summary>
    ///     Local-dev convenience: applies pending EF migrations at startup when the
    ///     AutoMigrate flag is set (the Aspire AppHost sets it; production never does).
    ///     Without the flag, drift is surfaced loudly instead of applied unsupervised —
    ///     a pending-migration list in the log beats an "Invalid object name" three
    ///     pages deep. Lives here (not Web) so EF stays behind the composition root.
    /// </summary>
    public static async Task ApplyOrReportMigrationsAsync(this IServiceProvider services, bool autoMigrate)
    {
        using var scope = services.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ChartAttemptDbContext>>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger(nameof(MigrationStartupExtensions));
        await using var database = await factory.CreateDbContextAsync();

        if (autoMigrate)
        {
            var pending = (await database.Database.GetPendingMigrationsAsync()).ToArray();
            if (pending.Length == 0) return;

            logger.LogInformation("AutoMigrate: applying {Count} pending migrations: {Migrations}",
                pending.Length, string.Join(", ", pending));
            await database.Database.MigrateAsync();
            return;
        }

        try
        {
            var pending = (await database.Database.GetPendingMigrationsAsync()).ToArray();
            if (pending.Length > 0)
                logger.LogWarning(
                    "{Count} EF migrations have not been applied to this database: {Migrations}. " +
                    "Apply them before relying on the affected features.",
                    pending.Length, string.Join(", ", pending));
        }
        catch (Exception e)
        {
            // Never block startup on the drift check — a DB that isn't reachable yet
            // fails later with a clearer error at the feature that needs it.
            logger.LogWarning(e, "Could not check for pending EF migrations at startup.");
        }
    }
}
