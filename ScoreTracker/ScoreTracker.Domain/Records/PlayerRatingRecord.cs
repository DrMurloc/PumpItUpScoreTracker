namespace ScoreTracker.Domain.Records
{
    public sealed record PlayerRatingRecord(Guid UserId, DateTimeOffset Date, double CompetitiveLevel,
        double SinglesLevel,
        double DoublesLevel, int CoOpRating, int PassCount)
    {
    }
}
