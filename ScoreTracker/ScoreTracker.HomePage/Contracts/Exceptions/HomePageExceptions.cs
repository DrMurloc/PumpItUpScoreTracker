namespace ScoreTracker.HomePage.Contracts.Exceptions;

/// <summary>A page or widget cap (D4) was hit. The UI disables add affordances at the cap; imports report this per item.</summary>
[ExcludeFromCodeCoverage]
public sealed class HomePageCapReachedException : Exception
{
    public HomePageCapReachedException(string message) : base(message)
    {
    }
}

/// <summary>The target page/widget doesn't exist for the current user — covers both deletion races and foreign ids.</summary>
[ExcludeFromCodeCoverage]
public sealed class HomePageNotFoundException : Exception
{
    public HomePageNotFoundException(string message) : base(message)
    {
    }
}

/// <summary>Shape validation failed (length caps, missing required fields). Messages are human-readable by design — import errors get pasted back into AI chats (D19).</summary>
[ExcludeFromCodeCoverage]
public sealed class HomePageValidationException : Exception
{
    public HomePageValidationException(string message) : base(message)
    {
    }
}
