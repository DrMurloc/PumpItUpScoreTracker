using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScoreTracker.Web.Services.Contracts;

namespace ScoreTracker.Web.Controllers;

/// <summary>
///     Persists a per-page presentation preference for the signed-in user (density today —
///     weekly-charts-overhaul.md §8, the /Mix/Set and /Culture/Set lineage). The static
///     challenges page swaps the view instantly in JS and fires this in the background, so it
///     returns 204 and never redirects. Keys are allowlisted by prefix; anonymous visitors get
///     the in-page toggle without persistence (this simply 401s for them, harmlessly).
/// </summary>
[Route("Preferences")]
[Authorize]
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class PreferencesController : Controller
{
    // Only presentation keys — nothing that gates access or carries data meaning.
    private static readonly string[] AllowedPrefixes = { "Density__" };

    private readonly IUiSettingsAccessor _settings;

    public PreferencesController(IUiSettingsAccessor settings)
    {
        _settings = settings;
    }

    [HttpPost("Set")]
    // A per-user UI preference over an allowlisted key: no sensitive state, so forging a request
    // could at most change someone's own density. Skipping the token keeps the background POST a
    // plain fetch with no page-embedded token to manage.
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Set([FromForm] string? key, [FromForm] string? value)
    {
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value) ||
            !AllowedPrefixes.Any(p => key.StartsWith(p, StringComparison.Ordinal)))
        {
            return BadRequest();
        }

        await _settings.SetSetting(key, value);
        return NoContent();
    }
}
