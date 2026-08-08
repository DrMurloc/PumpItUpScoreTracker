namespace ScoreTracker.Web.Configuration;

/// <summary>
///     The launch gate for chart comments.
///     <para>
///         <b>Off unless something turns it on.</b> The bool defaults false, so an absent section is
///         a closed gate; <c>appsettings.json</c> declares it anyway so the key exists somewhere a
///         person can find it. Locally it arrives from AppHost user-secrets through
///         <c>forwardedSections</c> — never <c>WithEnvironment</c>, which is read after user-secrets
///         and would override the setting it is meant to be controlled by.
///     </para>
///     <para>
///         While it is off the comment surfaces are the site admin's alone (<c>IsAdmin || Enabled</c>).
///         It governs <b>reading as well as writing</b>, which means flipping it publishes everything
///         written during testing at once. That is deliberate, and a clean-up pass before the flip is
///         a manual step rather than something the code does for you.
///     </para>
///     <para>
///         ⚠ <b>Personal notes are deliberately not gated.</b> Nothing in a note can go wrong in
///         public, and it is the one part of the feature that works on day one — comments need other
///         people before they are worth reading, a note to yourself works on an empty site.
///     </para>
/// </summary>
public sealed class ChartCommentsConfiguration
{
    public bool Enabled { get; set; }
}
