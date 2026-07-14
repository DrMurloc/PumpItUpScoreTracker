using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Identity.Application;
using ScoreTracker.Identity.Domain;
using ScoreTracker.Identity.Infrastructure;

namespace ScoreTracker.Identity.Wiring;

public static class IdentityRegistrationExtensions
{
    /// <summary>
    ///     Wires the Identity vertical (accounts, external logins, api tokens, UI settings,
    ///     content locks, account merges). EFUserRepository stays in ScoreTracker.Data
    ///     transitionally (the User table is still SQL-joined by other verticals), so the
    ///     reflective AddInfrastructure binding covers IUserRepository; Identity-owned ports
    ///     bind here. Handlers are discovered by the host's MediatR assembly scan.
    /// </summary>
    public static IServiceCollection AddIdentity(this IServiceCollection services)
    {
        services.AddTransient<IMergeRequestRepository, EFMergeRequestRepository>();
        services.AddTransient<IAccountPurgeRepository, EFAccountPurgeRepository>();
        services.AddTransient<IImportCredentialKeyStore, EFImportCredentialKeyStore>();
        services.AddTransient<IImportCredentialProtector, ImportCredentialProtector>();
        services.AddSingleton<IDbModelContribution, IdentityModelContribution>();
        return services;
    }

    /// <summary>
    ///     MassTransit's AddConsumers assembly scan skips internal types, so the vertical
    ///     registers its internal consumers explicitly through this hook — call it inside the
    ///     host's AddMassTransit block. Guarded by the consumer-discovery tripwire tests.
    /// </summary>
    public static void AddIdentityConsumers(this IRegistrationConfigurator configurator)
    {
        configurator.AddConsumer<AccountPurgeSaga>();
    }
}
