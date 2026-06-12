using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ScoreTracker.Data.Persistence;

// Lets `dotnet ef migrations add` run without booting the Web host (which requires
// real OAuth/Discord/SQL configuration at startup). Scaffolding never connects, so
// the connection string is a placeholder.
internal sealed class DesignTimeChartContextFactory : IDesignTimeDbContextFactory<ChartAttemptDbContext>
{
    public ChartAttemptDbContext CreateDbContext(string[] args)
    {
        return new ChartAttemptDbContext(new DbContextOptionsBuilder<ChartAttemptDbContext>()
            .UseSqlServer("Server=localhost;Database=DesignTimeOnly;Integrated Security=true;TrustServerCertificate=true")
            .Options);
    }
}
