using ScoreTracker.OfficialMirror.Contracts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Messaging;

namespace ScoreTracker.OfficialMirror.Contracts.Queries;

/// <summary>Recent sweep runs, newest first — the admin page's run-state table.</summary>
[ExcludeFromCodeCoverage]
public sealed record GetImportRunsQuery(MixEnum Mix, int Take = 10) : IQuery<IReadOnlyList<ImportRunRecord>>;
