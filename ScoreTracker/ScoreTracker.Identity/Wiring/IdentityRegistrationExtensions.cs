using Microsoft.Extensions.DependencyInjection;

namespace ScoreTracker.Identity.Wiring;

public static class IdentityRegistrationExtensions
{
    /// <summary>
    ///     Wires the Identity vertical (accounts, external logins, api tokens, UI settings,
    ///     content locks). EFUserRepository stays in ScoreTracker.Data transitionally (the
    ///     User table is still SQL-joined by other verticals), so the reflective
    ///     AddInfrastructure binding covers IUserRepository and nothing binds here yet.
    ///     Handlers are discovered by the host's MediatR assembly scan; the vertical has no
    ///     bus consumers, so there is no AddIdentityConsumers hook.
    /// </summary>
    public static IServiceCollection AddIdentity(this IServiceCollection services)
    {
        return services;
    }
}
