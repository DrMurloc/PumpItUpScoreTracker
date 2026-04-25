using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Records
{
    [ExcludeFromCodeCoverage]
    public sealed record PiuGameAccountDataImport(Uri AvatarUrl, Name AccountName, IEnumerable<Name> Titles, string Sid)
    {
    }
}
