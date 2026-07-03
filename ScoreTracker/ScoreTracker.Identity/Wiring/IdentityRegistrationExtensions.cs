using Microsoft.Extensions.DependencyInjection;
using ScoreTracker.Data.Persistence;
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
        services.AddSingleton<IDbModelContribution, IdentityModelContribution>();
        return services;
    }
}
