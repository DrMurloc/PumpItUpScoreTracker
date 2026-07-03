namespace ScoreTracker.Domain.Records
{
    [ExcludeFromCodeCoverage]
    public sealed record ExternalLoginRecord(string LoginProviderName, string ExternalId)
    {
    }
}
