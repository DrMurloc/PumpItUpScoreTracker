namespace ScoreTracker.Web.Services;

/// <summary>
///     Links to named community tools, for the handful of places the copy mentions one by name.
///     <para>
///         Hardcoded rather than read from the tool's registration row: this is one sentence on
///         two pages, and loading a tool to render a link would put a database read on the import
///         page's first paint. A tool that changes its address changes one line here.
///     </para>
/// </summary>
public static class CommunityToolLinks
{
    /// <summary>The one tool the import copy names, because it predates the directory.</summary>
    public const string PiuTracker = "https://piutracker.app";

    /// <summary>
    ///     An external tool link as markup, for a localized sentence that embeds it. Opens in a
    ///     new tab — the player is mid-import, and taking the page out from under them would cost
    ///     them the run in progress. <c>noopener</c> because the target is somebody else's site.
    /// </summary>
    public static string Anchor(string url, string text)
    {
        return $"<a href=\"{url}\" target=\"_blank\" rel=\"noopener noreferrer\" " +
               $"class=\"mud-link mud-primary-text mud-link-underline-hover\">{text}</a>";
    }

    /// <summary>An in-app link in the same shape, so both halves of a sentence look alike.</summary>
    public static string InternalAnchor(string href, string text)
    {
        return $"<a href=\"{href}\" class=\"mud-link mud-primary-text mud-link-underline-hover\">{text}</a>";
    }
}
