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
