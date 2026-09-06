using MediatR;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.EventCompetition.Contracts;
using ScoreTracker.EventCompetition.Contracts.Queries;
using ScoreTracker.EventCompetition.Domain;
using ScoreTracker.SharedKernel.Models;

namespace ScoreTracker.EventCompetition.Application;

/// <summary>
///     The My Sessions on-ramp (docs/design/march-of-murlocs.md D32). A quiet link sits under every
///     session, because there is always a season; the loud callout appears only when the night holds
///     a window the length of a session in which every counted play is one chart type and the rest
///     is under fifty minutes; and a night already on a board wears a chip instead of either.
///     <para>
///         MoM never links back to My Sessions. This is the one-way door in, and it is the only
///         place outside the section that knows a board exists.
///     </para>
/// </summary>
internal sealed class MoMOnRampHandler : IRequestHandler<DetectMoMSessionQuery, MoMOnRamp?>
{
    /// <summary>Under fifty minutes of rest inside the window is what makes a night a session (D32).</summary>
    private static readonly TimeSpan MaxRest = TimeSpan.FromMinutes(50);

    private readonly IMoMReadRepository _mom;
    private readonly IChartRepository _charts;
    private readonly IScoreReader _scores;
    private readonly IDateTimeOffsetAccessor _dateTime;

    public MoMOnRampHandler(IMoMReadRepository mom, IChartRepository charts, IScoreReader scores,
        IDateTimeOffsetAccessor dateTime)
    {
        _mom = mom;
        _charts = charts;
        _scores = scores;
        _dateTime = dateTime;
    }

    public async Task<MoMOnRamp?> Handle(DetectMoMSessionQuery request, CancellationToken cancellationToken)
    {
        var now = _dateTime.Now;
        var seasons = await _mom.GetSeasons(cancellationToken);
        var live = seasons.FirstOrDefault(s => s.StartsAt <= now && s.EndsAt >= now);
        if (live == null) return null;

        var boards = (await _mom.GetBoards(new[] { live.Id }, cancellationToken))
            .Where(b => b.Mix == request.Mix)
            .ToArray();
        if (boards.Length == 0) return null;

        var recorded = await Recorded(boards, request, cancellationToken);
        var candidate = recorded != null ? null : await Candidate(boards, request, cancellationToken);
        // The quiet link's board: the one the candidate names, else Doubles, else whatever exists.
        var fallback = boards.FirstOrDefault(b => b.ChartType == ChartTypeOf(candidate))
                       ?? boards.FirstOrDefault(b => b.ChartType == SharedKernel.Enums.ChartType.Double)
                       ?? boards[0];

        return new MoMOnRamp(fallback.Id, recorded, candidate);
    }

    private static SharedKernel.Enums.ChartType? ChartTypeOf(MoMSessionCandidate? candidate) => candidate?.ChartType;

    /// <summary>
    ///     A published session of this player's whose charts were played inside this night. Matched
    ///     on the import's stamps, so a hand-entered session carries none and never claims a night
    ///     it may not be.
    /// </summary>
    private async Task<MoMRecordedNight?> Recorded(IReadOnlyList<MoMBoardInfo> boards,
        DetectMoMSessionQuery request, CancellationToken cancellationToken)
    {
        var published = (await _mom.GetPublishedSessions(boards.Select(b => b.Id), cancellationToken)).ToArray();
        var mine = published.Where(s => s.UserId == request.UserId).ToArray();
        if (mine.Length == 0) return null;

        var rows = await _mom.GetSessionCharts(mine.Select(s => s.Id), cancellationToken);
        var fromNight = rows
            .Where(r => r.PlayedAt is { } at && at >= request.From && at <= request.To)
            .Select(r => r.SessionId)
            .ToHashSet();
        var match = mine.FirstOrDefault(s => fromNight.Contains(s.Id));
        if (match == null) return null;

        // Its place on its own board, which is what the chip carries.
        var board = boards.First(b => b.Id == match.BoardId);
        var onBoard = published.Where(s => s.BoardId == board.Id)
            .OrderByDescending(s => s.TotalScore)
            .ThenBy(s => s.PublishedAt)
            .ToArray();
        var place = Array.FindIndex(onBoard, s => s.Id == match.Id) + 1;
        return new MoMRecordedNight(match.Id, board.ChartType, place, onBoard.Length, match.TotalScore);
    }

    private async Task<MoMSessionCandidate?> Candidate(IReadOnlyList<MoMBoardInfo> boards,
        DetectMoMSessionQuery request, CancellationToken cancellationToken)
    {
        var entries = await _scores.GetRecentPlays(request.Mix, request.UserId, request.From,
            JournalLimit, cancellationToken);
        var inNight = entries.Where(e => e.OccurredAt <= request.To).ToArray();
        if (inNight.Length == 0) return null;

        var charts = (await _charts.GetCharts(request.Mix,
                chartIds: inNight.Select(e => e.ChartId).Distinct().ToArray(),
                cancellationToken: cancellationToken))
            .ToDictionary(c => c.Id);
        var plays = inNight.Where(e => charts.ContainsKey(e.ChartId))
            .OrderBy(e => e.OccurredAt)
            .Select(e => new MoMPlay(e.ChartId, e.OccurredAt, charts[e.ChartId].Song.Duration,
                charts[e.ChartId].Type, e.IsStageBroken))
            .ToArray();
        if (plays.Length == 0) return null;

        // The window is the board's own, so a board with a different one would be honoured.
        var window = boards[0].Configuration.MaxTime;
        var found = MoMSessionDetector.FindSessionWindow(plays, window, MaxRest);
        if (found == null) return null;

        var board = boards.FirstOrDefault(b => b.ChartType == found.Type);
        if (board == null) return null;

        var stageBreaks = plays.Skip(found.StartIndex).Take(found.EndIndex - found.StartIndex + 1)
            .Count(p => p.IsStageBroken);
        return new MoMSessionCandidate(board.Id, found.Type, found.Charts, found.SongTime, found.Rest,
            plays[found.StartIndex].PlayedAt, plays[found.EndIndex].EndsAt, stageBreaks);
    }

    /// <summary>A night is at most a few hours; this is the ceiling, not the expectation.</summary>
    private const int JournalLimit = 300;
}
