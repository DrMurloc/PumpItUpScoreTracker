using MediatR;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.EventCompetition.Contracts.Commands;

/// <summary>
///     Enters one play by hand. A chart already in the session is not an error (D45): the better
///     score stays and the result says which.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record AddMoMDraftChartCommand(
    Guid SessionId,
    Guid ChartId,
    PhoenixScore Score,
    PhoenixPlate Plate,
    bool IsBroken) : IRequest<MoMEntryResult>;
