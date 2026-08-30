using MediatR;
using ScoreTracker.Catalog.Contracts;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.Catalog.Domain;

namespace ScoreTracker.Catalog.Application;

/// <summary>
///     Decodes the banked payload behind the per-chart cache and projects the asking mix's
///     view. Every nothing — never banked, unreadable payload, no verdict for the mix,
///     Excluded — answers null, one shape for "no section renders".
/// </summary>
internal sealed class GetChartStepChartHandler(IChartStepChartRepository stepCharts)
    : IRequestHandler<GetChartStepChartQuery, ChartStepChartRecord?>
{
    public async Task<ChartStepChartRecord?> Handle(GetChartStepChartQuery request,
        CancellationToken cancellationToken)
    {
        var banked = await stepCharts.Get(request.ChartId, cancellationToken);
        if (banked == null) return null;
        var payload = StepChartPayloadCodec.Decode(banked.Payload);
        if (payload == null) return null;
        var verdict = StepChartPayloadCodec.VerdictFor(payload, request.Mix);
        if (verdict == null || verdict.Visibility == StepChartVisibility.Excluded) return null;

        return new ChartStepChartRecord(
            banked.Vintage,
            payload.Panels,
            payload.Aligned,
            verdict.Visibility,
            verdict.NoteCount,
            verdict.ImpliedTotal,
            payload.Rows.Select(r => new StepChartRowRecord(r.T, r.M, r.L, r.Q, r.B)).ToArray(),
            payload.Holds.Select(h => new StepChartHoldRecord(h.P, h.S, h.E, h.L)).ToArray(),
            payload.Ticks,
            payload.Segments.Select(s => new StepChartSegmentRecord(s.S, s.E, s.N)).ToArray(),
            payload.Ranges.Select(r => new StepChartRangeRecord(r.S, r.E)).ToArray());
    }
}
