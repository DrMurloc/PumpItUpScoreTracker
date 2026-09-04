namespace ScoreTracker.Domain.Exceptions;

/// <summary>
///     A domain exception, so its message is safe to show a player (DiagnosticExposureTests) —
///     it is written to be the sentence they read, not a diagnostic.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class InvalidAvatarException : Exception
{
    public InvalidAvatarException() : base("That is not an avatar you can choose.")
    {
    }
}
