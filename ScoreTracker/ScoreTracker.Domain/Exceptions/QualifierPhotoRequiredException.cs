namespace ScoreTracker.Domain.Exceptions;

[ExcludeFromCodeCoverage]
public sealed class QualifierPhotoRequiredException : Exception
{
    public QualifierPhotoRequiredException() : base(
        "A qualifier score you enter yourself needs a photo of the result screen. Scores imported from the official site do not.")
    {
    }
}
