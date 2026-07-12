namespace ScoreTracker.Domain.Records
{
    // SkillRating (PUMBILITY) is nullable: capture started 2026-07 — rows from before then never
    // recorded it, and null (not 0) is how consumers distinguish "not captured" from a real zero.
    [ExcludeFromCodeCoverage]
    public sealed record PlayerRatingRecord(Guid UserId, DateTimeOffset Date, double CompetitiveLevel,
        double SinglesLevel,
        double DoublesLevel, int CoOpRating, int PassCount, int? SkillRating = null)
    {
    }
}
