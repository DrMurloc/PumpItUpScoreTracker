using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Records
{
    public sealed record CountryRecord(Name Name, Uri ImagePath)
    {
    }
}
