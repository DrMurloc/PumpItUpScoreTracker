using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ScoreTracker.Data.Apis;
using ScoreTracker.Data.Apis.Contracts;
using ScoreTracker.Data.Clients;
using ScoreTracker.Data.Configuration;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Data.Repositories;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.CompositionRoot;

public static class RegistrationExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection builder,
        AzureBlobConfiguration blobConfig, SqlConfiguration configuration, SendGridConfiguration twilioConfig)
    {
        foreach (var implementationType in typeof(EFChartRepository).Assembly.GetTypes()
                )
        foreach (var interfaceType in implementationType.GetInterfaces()
                     .Where(i => i.Assembly == typeof(IChartRepository).Assembly && i != typeof(IBotClient)))
            builder.AddTransient(interfaceType, implementationType);

        builder.AddSingleton<IBotClient, DiscordBotClient>();
        builder.AddTransient<IDbContextFactory<ChartAttemptDbContext>, ChartDbContextFactory>();
        builder.Configure<SendGridConfiguration>(o =>
        {
            o.FromEmail = twilioConfig.FromEmail;
            o.ToEmail = twilioConfig.ToEmail;
            o.ApiKey = twilioConfig.ApiKey;
        });
        builder.AddHttpClient<IPiuGameApi, PiuGameApi>(c =>
        {
            c.DefaultRequestHeaders.Add("Origin", "https://piugame.com");
        });
        builder.Configure<SqlConfiguration>(o => { o.ConnectionString = configuration.ConnectionString; });
        builder.Configure<AzureBlobConfiguration>(o => { o.ConnectionString = blobConfig.ConnectionString; });

        return builder.AddDbContext<ChartAttemptDbContext>(o => { o.UseSqlServer(configuration.ConnectionString); });
    }
}