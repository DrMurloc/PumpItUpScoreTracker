namespace ScoreTracker.Domain.Exceptions;

[ExcludeFromCodeCoverage]
public sealed class CannotRemoveLastExternalLoginException : Exception
{
    public CannotRemoveLastExternalLoginException() : base(
        "An account must keep at least one sign-in method; the last external login cannot be removed.")
    {
    }
}
