using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Domain.Records
{
    [ExcludeFromCodeCoverage]
    public sealed record CountryRecord(Name Name, Uri ImagePath)
    {
    }
}
