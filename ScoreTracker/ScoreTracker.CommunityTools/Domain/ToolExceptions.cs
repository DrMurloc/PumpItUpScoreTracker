namespace ScoreTracker.CommunityTools.Domain;

/// <summary>
///     Base for Community Tools rule violations. Messages here are written to be read by the person
///     who tripped them — a maker or a player — which is what keeps them showable
///     (<c>DiagnosticExposureTests</c> allows a domain exception's message through precisely because
///     it is copy, not diagnostics).
/// </summary>
[ExcludeFromCodeCoverage]
internal abstract class CommunityToolsException : Exception
{
    protected CommunityToolsException(string message) : base(message)
    {
    }
}

[ExcludeFromCodeCoverage]
internal sealed class ToolListingException : CommunityToolsException
{
    public ToolListingException(string message) : base(message)
    {
    }
}

[ExcludeFromCodeCoverage]
internal sealed class ToolWebhookModeException : CommunityToolsException
{
    public ToolWebhookModeException(string message) : base(message)
    {
    }
}

[ExcludeFromCodeCoverage]
internal sealed class ToolShareException : CommunityToolsException
{
    public ToolShareException(string message) : base(message)
    {
    }
}

/// <summary>
///     The tool changed what it asks for between the dialog rendering and the player pressing the
///     button. The message names the change rather than saying "try again", because the player is
///     about to be shown a much more serious warning and the reason for it should not be a surprise.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed class ToolConsentMismatchException : CommunityToolsException
{
    public ToolConsentMismatchException() : base(
        "This tool now asks for your PIUGame session, not just your scores. " +
        "Nothing was shared — open it again to see what that means.")
    {
    }
}

/// <summary>
///     The tool's maker has no Discord account linked, so the tool cannot take anyone but its maker.
///     A linked account is how DrMurloc reaches a maker when something goes wrong.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed class ToolDiscordRequiredException : CommunityToolsException
{
    private ToolDiscordRequiredException(string message) : base(message)
    {
    }

    /// <summary>What a player sees. Names the tool, and says nothing about whose fault it is.</summary>
    public static ToolDiscordRequiredException ForPlayer(string toolName)
    {
        return new ToolDiscordRequiredException(
            $"{toolName} can't connect players yet. Its maker still has to link a Discord account, so " +
            "DrMurloc can reach them if anything goes wrong. Nothing was shared.");
    }

    /// <summary>What the maker sees, which is the same rule from the side that can fix it.</summary>
    public static ToolDiscordRequiredException ForMaker()
    {
        return new ToolDiscordRequiredException(
            "Link your Discord first, from your tool's settings. It's how DrMurloc reaches you if " +
            "anything goes wrong, for your players or for you.");
    }
}

/// <summary>
///     A tool the caller may not touch, or that does not exist. One exception for both so a
///     probe cannot distinguish "not yours" from "not there".
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed class ToolNotFoundException : CommunityToolsException
{
    public ToolNotFoundException() : base("That tool doesn't exist, or isn't yours.")
    {
    }
}
