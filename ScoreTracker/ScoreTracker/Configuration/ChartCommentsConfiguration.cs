namespace ScoreTracker.Web.Configuration;

/// <summary>
///     The launch gate for chart comments.
///     <para>
///         Off in production until cost testing says otherwise, so the comment surfaces are the site
///         admin's alone — <c>IsAdmin || Enabled</c>. It governs <b>reading as well as writing</b>,
///         which means flipping it publishes everything written during testing at once. That is
///         deliberate, and a clean-up pass before the flip is a manual step rather than something
///         the code does for you.
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
