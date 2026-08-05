using Microsoft.Extensions.DependencyInjection;

namespace ScoreTracker.Translations.Wiring;

/// <summary>
///     Wires the Translations vertical.
///     <para>
///         There is nothing to register. The handler is found by the host's MediatR assembly
///         scan, the vertical owns no tables so it contributes no EF model, and it has no bus
///         consumers — so there is no <c>AddTranslationsConsumers</c> hook either. The one
///         dependency, <c>ILanguageModelClient</c>, is deliberately left to the caller: nothing
///         in the running application should spend metered tokens by accident, so the only thing
///         that supplies an implementation today is the workbench in ScoreTracker.ExplorationTests.
///     </para>
///     <para>
///         The method exists anyway. It is the assembly marker the architecture tests anchor on,
///         and it is where registration goes the day the comments feature makes this vertical
///         real.
///     </para>
/// </summary>
public static class TranslationsRegistrationExtensions
{
    public static IServiceCollection AddTranslations(this IServiceCollection services)
    {
        return services;
    }
}
