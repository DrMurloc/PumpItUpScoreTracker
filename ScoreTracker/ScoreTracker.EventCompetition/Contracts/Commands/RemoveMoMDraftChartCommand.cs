using MediatR;

namespace ScoreTracker.EventCompetition.Contracts.Commands;

/// <summary>Takes one chart back out of a draft, by its position in the list.</summary>
[ExcludeFromCodeCoverage]
public sealed record RemoveMoMDraftChartCommand(Guid SessionId, int Ordinal) : IRequest;
