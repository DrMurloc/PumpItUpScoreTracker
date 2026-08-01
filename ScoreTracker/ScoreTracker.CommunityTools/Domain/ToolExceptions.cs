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
