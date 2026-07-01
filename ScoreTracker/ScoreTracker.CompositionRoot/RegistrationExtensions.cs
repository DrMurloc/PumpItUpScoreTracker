using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ScoreTracker.Catalog.Wiring;
using ScoreTracker.ChartIntelligence.Wiring;
using ScoreTracker.Data.Clients;
using ScoreTracker.Data.Configuration;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Data.Repositories;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.EventCompetition.Wiring;
using ScoreTracker.OfficialMirror.Wiring;
using ScoreTracker.ScoreLedger.Wiring;
using ScoreTracker.Ucs.Wiring;
using ScoreTracker.WeeklyChallenge.Wiring;

namespace ScoreTracker.CompositionRoot;

public static class RegistrationExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection builder,
        AzureBlobConfiguration blobConfig, SqlConfiguration configuration, SendGridConfiguration twilioConfig)
    {
        foreach (var implementationType in typeof(EFUserRepository).Assembly.GetTypes()
                )
        foreach (var interfaceType in implementationType.GetInterfaces()
                     .Where(i => i.Assembly == typeof(IChartRepository).Assembly && i != typeof(IBotClient)))
            builder.AddTransient(interfaceType, implementationType);

        builder.AddSingleton<IBotClient, DiscordBotClient>();
        builder.Configure<SendGridConfiguration>(o =>
        {
            o.FromEmail = twilioConfig.FromEmail;
            o.ToEmail = twilioConfig.ToEmail;
            o.ApiKey = twilioConfig.ApiKey;
        });
        builder.Configure<SqlConfiguration>(o => { o.ConnectionString = configuration.ConnectionString; });
        builder.Configure<AzureBlobConfiguration>(o => { o.ConnectionString = blobConfig.ConnectionString; });

        builder.AddCatalog();
        builder.AddChartIntelligence();
        builder.AddEventCompetition();
        builder.AddOfficialMirror();
        builder.AddScoreLedger();
        builder.AddUcs();
        builder.AddWeeklyChallenge();

        // Not pooled: pooling requires an options-only constructor, and the context takes
        // the verticals' IDbModelContribution set from DI (ADR-001 D4).
        return builder.AddDbContextFactory<ChartAttemptDbContext>(o =>
        {
            o.UseSqlServer(configuration.ConnectionString);
        });
    }
}
