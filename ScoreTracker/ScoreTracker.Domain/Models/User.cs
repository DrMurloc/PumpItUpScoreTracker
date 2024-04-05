using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Models;

public sealed record User(Guid Id, Name Name, bool IsPublic, Name? GameTag, Uri ProfileImage)
{
    private static readonly Guid DrMurlocGuid = Guid.Parse("E38954C4-B1B1-418A-93F6-C4B25C98B713");

    public bool IsAdmin => Id == DrMurlocGuid;
}