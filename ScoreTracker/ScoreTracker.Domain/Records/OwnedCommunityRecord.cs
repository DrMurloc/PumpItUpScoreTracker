using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Domain.Records;

/// <summary>
///     A community the player created, as the deletion blocker lists it. MemberCount is what
///     decides whether they are offered a hand-over or a delete: a community with nobody else in
///     it has no one to hand it to.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record OwnedCommunityRecord(Name CommunityName, int MemberCount, int AdminCount);
