using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using ScoreTracker.CommunityTools.Application;
using ScoreTracker.CommunityTools.Domain;
using ScoreTracker.CommunityTools.Infrastructure;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.CommunityTools.Wiring;

public static class CommunityToolsRegistrationExtensions
{
    /// <summary>
    ///     Wires the Community Tools vertical: tool registration, player shares, API keys and
    ///     webhook delivery. Handlers are discovered by the host's MediatR assembly scan; bus
    ///     consumers are NOT — see <see cref="AddCommunityToolsConsumers" />.
    /// </summary>
    public static IServiceCollection AddCommunityTools(this IServiceCollection services)
    {
        services.AddTransient<IToolRepository, EFToolRepository>();
        services.AddTransient<IToolKeyRepository, EFToolKeyRepository>();
        services.AddTransient<IWebhookDeliveryRepository, EFWebhookDeliveryRepository>();
        services.AddTransient<IToolSecretProtector, ToolSecretProtector>();
        services.AddTransient<IToolSecretReader, EFToolSecretReader>();
        services.AddTransient<IToolActivityRepository, EFToolActivityRepository>();
        services.AddTransient<IWebhookDeliveryDispatcher, WebhookDeliveryDispatcher>();
        // A typed client, so the vertical owns its own outbound policy rather than borrowing one.
        services.AddHttpClient<IWebhookDeliveryClient, WebhookDeliveryClient>();
        services.AddTransient<IAccountPurgeRepository, EFAccountPurgeRepository>();
        // The Domain port OfficialMirror hands a live piugame session to. Registered here rather
        // than by the CompositionRoot's reflection pass, which only scans ScoreTracker.Data.
        services.AddTransient<ISessionDeliveryClient, SessionDeliveryClient>();
        services.AddSingleton<IDbModelContribution, CommunityToolsModelContribution>();
        // Bound by the host; the defaults are the safe ones, so a missing section is not a hole.
        services.AddOptions<CommunityToolsConfiguration>();
        return services;
    }

    /// <summary>
    ///     MassTransit's AddConsumers assembly scan skips internal types, so the vertical registers
    ///     its internal consumers explicitly through this hook — call it inside the host's
    ///     AddMassTransit block. Guarded by the tripwire in VerticalBoundaryTests.
    /// </summary>
    public static void AddCommunityToolsConsumers(this IRegistrationConfigurator configurator)
    {
        configurator.AddConsumer<AccountPurgeConsumer>();
        configurator.AddConsumer<WebhookDeliverySaga>();
        configurator.AddConsumer<WebhookMaintenanceSaga>();
    }
}
