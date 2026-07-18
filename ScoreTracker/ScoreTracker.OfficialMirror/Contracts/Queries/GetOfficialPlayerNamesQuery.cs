using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Messaging;

namespace ScoreTracker.OfficialMirror.Contracts.Queries;

/// <summary>Every known board tag for the mix (departed players included — history is searchable).</summary>
[ExcludeFromCodeCoverage]
public sealed record GetOfficialPlayerNamesQuery(MixEnum Mix) : IQuery<IReadOnlyList<string>>;
