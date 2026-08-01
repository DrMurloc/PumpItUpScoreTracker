using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using ScoreTracker.Data.Persistence;

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
        services.AddSingleton<IDbModelContribution, CommunityToolsModelContribution>();
        return services;
    }

    /// <summary>
    ///     MassTransit's AddConsumers assembly scan skips internal types, so the vertical registers
    ///     its internal consumers explicitly through this hook — call it inside the host's
    ///     AddMassTransit block. Guarded by the tripwire in VerticalBoundaryTests.
    /// </summary>
    public static void AddCommunityToolsConsumers(this IRegistrationConfigurator configurator)
    {
    }
}
