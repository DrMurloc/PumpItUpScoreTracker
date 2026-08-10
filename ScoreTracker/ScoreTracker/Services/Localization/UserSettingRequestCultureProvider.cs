using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Caching.Memory;
using ScoreTracker.Domain.Records;
using ScoreTracker.Identity.Contracts.Queries;

namespace ScoreTracker.Web.Services.Localization;

/// <summary>
///     Rank 2 of the resolution order (docs/design/culture-resolution.md): for a signed-in
///     player, the language they chose on /Account is the answer. It outranks the cookie and
///     the browser, so an account set to English renders English on a Spanish browser, on every
///     device, forever — the cookie is only a cache and an anonymous visitor's only store.
///     <para>
///         Registered between <c>UseAuthentication</c> and <c>UseAuthorization</c>, where
///         <see cref="HttpContext.User" /> is the cookie principal or nobody. An api/* caller
///         authenticating with an ApiToken is anonymous here on purpose: machine output should
///         not change language with whoever owns the key.
///     </para>
/// </summary>
public sealed class UserSettingRequestCultureProvider : RequestCultureProvider
{
    public const string SettingKey = "Culture";

    public override async Task<ProviderCultureResult?> DetermineProviderCultureResult(HttpContext httpContext)
    {
        var userId = SignedInUserId(httpContext);
        if (userId == null) return null;

        var settings = await Settings(httpContext, userId.Value);
        // ResolveClosest rather than NormalizeOrNull: a stored value is not always one of the
        // nine exact tags. It can predate the es → es-ES split, or carry whitespace, and a row
        // reading "es" that resolves to nothing would drop that player back to their browser —
        // the exact symptom this provider exists to fix. Genuine nonsense still returns null.
        var saved = settings.TryGetValue(SettingKey, out var stored)
            ? SupportedCultures.ResolveClosest(stored)
            : null;

        // Declining leaves the rest of the chain exactly as it was: cookie, then browser.
        return saved == null ? null : new ProviderCultureResult(saved);
    }

    private static Guid? SignedInUserId(HttpContext httpContext)
    {
        if (httpContext.User.Identity?.IsAuthenticated != true) return null;

        return Guid.TryParse(httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id)
            ? id
            : null;
    }

    /// <summary>
    ///     The same cache entry the shell reads, so a normal request costs no query, and a
    ///     language change is visible on the next one because saving any setting evicts it.
    ///     A failure here is never worth an error page — this middleware now runs on every
    ///     request, static assets included — so the language falls back to the cookie and the
    ///     browser instead. A genuinely aborted request still propagates.
    /// </summary>
    private static async Task<IDictionary<string, string>> Settings(HttpContext httpContext, Guid userId)
    {
        try
        {
            var cache = httpContext.RequestServices.GetRequiredService<IMemoryCache>();
            return (await cache.GetOrCreateAsync(ShellModelFactory.SettingsCacheKey(userId), entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = ShellModelFactory.SettingsTtl;
                return httpContext.RequestServices.GetRequiredService<IMediator>()
                    .Send(new GetUserUiSettingsQuery(userId), httpContext.RequestAborted);
            }))!;
        }
        catch (Exception e) when (!httpContext.RequestAborted.IsCancellationRequested)
        {
            httpContext.RequestServices.GetRequiredService<ILogger<UserSettingRequestCultureProvider>>()
                .LogWarning(e, "Could not read the saved language for {UserId}", userId);
            return new Dictionary<string, string>();
        }
    }
}
