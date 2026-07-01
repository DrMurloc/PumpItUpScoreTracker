using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using ScoreTracker.Data.Persistence;

namespace ScoreTracker.CompositionRoot;

// Lets `dotnet ef migrations add` run without booting the Web host (which requires
// real OAuth/Discord/SQL configuration at startup). Scaffolding never connects, so
// the connection string is a placeholder. Lives in CompositionRoot (not Data) because
// the design-time model must include every vertical's IDbModelContribution — run
// scaffolding with `--startup-project ../ScoreTracker.CompositionRoot` from Data.
internal sealed class DesignTimeChartContextFactory : IDesignTimeDbContextFactory<ChartAttemptDbContext>
{
    public ChartAttemptDbContext CreateDbContext(string[] args)
    {
        return new ChartAttemptDbContext(new DbContextOptionsBuilder<ChartAttemptDbContext>()
                .UseSqlServer(
                    "Server=localhost;Database=DesignTimeOnly;Integrated Security=true;TrustServerCertificate=true")
                .Options,
            VerticalModelContributions.All());
    }
}
