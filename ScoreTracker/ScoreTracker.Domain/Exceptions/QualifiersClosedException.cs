namespace ScoreTracker.Domain.Exceptions;

[ExcludeFromCodeCoverage]
public sealed class QualifiersClosedException : Exception
{
    public QualifiersClosedException() : base("Qualifiers for this tournament have closed.")
    {
    }
}
