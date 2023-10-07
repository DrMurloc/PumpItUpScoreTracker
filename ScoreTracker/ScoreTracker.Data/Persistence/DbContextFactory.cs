using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ScoreTracker.Data.Configuration;

namespace ScoreTracker.Data.Persistence
{
    public class ChartDbContextFactory : IDbContextFactory<ChartAttemptDbContext>
    {
        private readonly DbContextOptions<ChartAttemptDbContext> _options;
        private readonly IOptions<SqlConfiguration> _sqlConfig;

        public ChartDbContextFactory(DbContextOptions<ChartAttemptDbContext> options,
            IOptions<SqlConfiguration> sqlConfig)
        {
            _options = options;
            _sqlConfig = sqlConfig;
        }

        public ChartAttemptDbContext CreateDbContext()
        {
            return new ChartAttemptDbContext(_options, _sqlConfig);
        }
    }
}
