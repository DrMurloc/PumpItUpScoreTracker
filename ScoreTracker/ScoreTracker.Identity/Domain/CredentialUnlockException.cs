namespace ScoreTracker.Identity.Domain;

internal sealed class CredentialUnlockException : Exception
{
    public CredentialUnlockException(string message) : base(message)
    {
    }

    public CredentialUnlockException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
