namespace ScoreTracker.Domain.Exceptions;

/// <summary>
///     A March of Murlocs session rule refused the request — the message is the player-facing
///     sentence (an ended season, a published session being edited, an empty draft being
///     published).
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class MoMSessionRuleException : Exception
{
    public MoMSessionRuleException(string message) : base(message)
    {
    }
}
