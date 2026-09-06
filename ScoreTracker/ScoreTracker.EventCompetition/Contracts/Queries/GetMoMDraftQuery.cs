using ScoreTracker.SharedKernel.Messaging;

namespace ScoreTracker.EventCompetition.Contracts.Queries;

/// <summary>
///     One session as Submit renders it. Null when it does not exist or belongs to someone else — a
///     draft is private, and a published session is read through the Breakdown instead.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetMoMDraftQuery(Guid SessionId) : IQuery<MoMDraftView?>;
