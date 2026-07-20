using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Messaging;

namespace ScoreTracker.OfficialMirror.Contracts.Queries;

/// <summary>The board tag linked to a site account on this mix, if any — links land at import/claim time.</summary>
[ExcludeFromCodeCoverage]
public sealed record GetLinkedOfficialPlayerTagQuery(MixEnum Mix, Guid UserId) : IQuery<string?>;
