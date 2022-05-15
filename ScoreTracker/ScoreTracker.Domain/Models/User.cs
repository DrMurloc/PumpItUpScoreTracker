using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Models;

public sealed record User(Guid Id, Name Name, bool IsPublic)
{
}