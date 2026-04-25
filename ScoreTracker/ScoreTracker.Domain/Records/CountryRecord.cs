using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Records
{
    [ExcludeFromCodeCoverage]
    public sealed record CountryRecord(Name Name, Uri ImagePath)
    {
    }
}
