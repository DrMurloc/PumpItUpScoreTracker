using MediatR;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.EventCompetition.Contracts;
using ScoreTracker.EventCompetition.Contracts.Commands;
using ScoreTracker.EventCompetition.Contracts.Queries;
using ScoreTracker.EventCompetition.Domain;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.EventCompetition.Application;

/// <summary>
///     Filling a draft from the score journal (§11.4). Since slice 3 this reads the journal rather
///     than the official site, so the page has no credential field at all — the standing "Submit
///     never stores a password" rule is vacuous in the strongest way: it never sees one.
///     <para>
///         Freshness is Import Scores' job. A night missing from the journal means running that page
///         first and coming back, which is why nothing here triggers an import: the standing trap is
///         that <c>ScoreImportCompletedEvent</c> publishes before the journal write lands, so acting
///         on it reads the pre-import state.
///     </para>
/// </summary>
internal sealed partial class MoMDraftHandler :
    IRequestHandler<GetMoMImportCandidatesQuery, MoMImportCandidates?>,
    IRequestHandler<ImportMoMDraftFromJournalCommand, MoMImportResult>
{
    /// <summary>How far back the dialog looks. A session is one night; a week covers a forgotten one.</summary>
    private static readonly TimeSpan JournalWindow = TimeSpan.FromDays(7);

    private const int JournalLimit = 300;

    public async Task<MoMImportCandidates?> Handle(GetMoMImportCandidatesQuery request,
        CancellationToken cancellationToken)
    {
        var loaded = await LoadDraft(request.SessionId, cancellationToken);
        if (loaded == null) return null;

        var (plays, journal, charts) = await RecentPlays(loaded, cancellationToken);
        var window = loaded.Board.Configuration.MaxTime;
        var type = loaded.Board.ChartType;
        if (plays.Count == 0)
            return new MoMImportCandidates(type, window, Array.Empty<MoMImportPlay>(),
                Array.Empty<MoMImportBlock>(), 0, -1, Checks(plays, charts, 0, -1, type, window, loaded));

        var blocks = MoMSessionDetector.Split(plays);
        var suggested = MoMSessionDetector.Suggest(plays, type, window) ?? blocks[^1];
        var start = request.StartIndex ?? suggested.StartIndex;
        var end = request.EndIndex ?? suggested.EndIndex;

        return new MoMImportCandidates(
            type,
            window,
            plays.Select((p, i) => new MoMImportPlay(i, charts[p.ChartId], p.PlayedAt, journal[i].Score,
                journal[i].Plate, journal[i].IsBroken, p.IsStageBroken, !p.IsStageBroken && p.Type != type))
                .ToArray(),
            blocks.Select((b, i) => new MoMImportBlock(b.StartIndex, b.EndIndex, b.From, b.To, b.Plays,
                    Song(plays, b.StartIndex, b.EndIndex),
                    Rest(plays, b.StartIndex, b.EndIndex),
                    plays.Skip(b.StartIndex).Take(b.Plays).Select(p => p.Type).Distinct().Count() > 1,
                    i == 0 ? null : MoMSessionDetector.GapBefore(plays, b.StartIndex)))
                .ToArray(),
            start,
            end,
            Checks(plays, charts, start, end, type, window, loaded));
    }

    public async Task<MoMImportResult> Handle(ImportMoMDraftFromJournalCommand request,
        CancellationToken cancellationToken)
    {
        var empty = new MoMImportResult(0, 0, 0, 0, Array.Empty<MoMReplacedPlay>());
        var loaded = await LoadDraft(request.SessionId, cancellationToken);
        if (loaded == null) return empty;

        var (plays, journal, charts) = await RecentPlays(loaded, cancellationToken);
        var type = loaded.Board.ChartType;

        int added = 0, replaced = 0, kept = 0, skipped = 0;
        var replacements = new List<MoMReplacedPlay>();
        for (var i = 0; i < plays.Count; i++)
        {
            var play = plays[i];
            if (play.PlayedAt < request.From || play.PlayedAt > request.To) continue;

            var entry = journal[i];
            // A stage break scores nothing (D40), the other board's charts are not ours, and a play
            // the journal never scored has nothing to enter.
            if (play.IsStageBroken || play.Type != type || entry.Score is not { } score)
            {
                skipped++;
                continue;
            }

            var chart = charts[play.ChartId];
            if (!loaded.Session.CanAdd(chart))
            {
                skipped++;
                continue;
            }

            var previous = Held(loaded.Session, chart)?.Score;
            switch (loaded.Session.Add(chart, score, entry.Plate ?? PhoenixPlate.RoughGame, entry.IsBroken,
                        play.PlayedAt))
            {
                case TournamentSession.AddOutcome.Added:
                    added++;
                    break;
                case TournamentSession.AddOutcome.Replaced:
                    replaced++;
                    if (previous is { } was) replacements.Add(new MoMReplacedPlay(chart.Id, was));
                    break;
                default:
                    kept++;
                    break;
            }
        }

        if (added + replaced > 0)
            await _write.SaveSession(request.SessionId, loaded.Session, cancellationToken);

        return new MoMImportResult(added, replaced, kept, skipped, replacements);
    }

    /// <summary>
    ///     The player's recent plays, oldest first, as the detector sees them and as the journal
    ///     wrote them. Plays whose chart the catalog cannot resolve are dropped rather than guessed
    ///     at: the dialog only draws what it can name.
    /// </summary>
    private async Task<(IReadOnlyList<MoMPlay> Plays, IReadOnlyList<ScoreJournalEntry> Journal,
            IReadOnlyDictionary<Guid, Chart> Charts)>
        RecentPlays(LoadedSession loaded, CancellationToken cancellationToken)
    {
        var entries = await _scores.GetRecentPlays(loaded.Board.Mix, loaded.Stored.UserId,
            _dateTime.Now - JournalWindow, JournalLimit, cancellationToken);
        var charts = (await _charts.GetCharts(loaded.Board.Mix,
                chartIds: entries.Select(e => e.ChartId).Distinct().ToArray(),
                cancellationToken: cancellationToken))
            .ToDictionary(c => c.Id);

        var usable = entries.Where(e => charts.ContainsKey(e.ChartId))
            .OrderBy(e => e.OccurredAt)
            .ToArray();
        var plays = usable.Select(e =>
            {
                var chart = charts[e.ChartId];
                return new MoMPlay(e.ChartId, e.OccurredAt, chart.Song.Duration, chart.Type, e.IsStageBroken);
            })
            .ToArray();

        return (plays, usable, charts);
    }

    private MoMImportChecks Checks(IReadOnlyList<MoMPlay> plays, IReadOnlyDictionary<Guid, Chart> charts,
        int start, int end, ChartType type, TimeSpan window, LoadedSession loaded)
    {
        if (plays.Count == 0 || end < start)
            return new MoMImportChecks(0, 0, TimeSpan.Zero, false, TimeSpan.Zero, false, null, null, null,
                Array.Empty<Name>(), 0, 0);

        var checks = MoMSessionDetector.Check(plays, start, end, type, window);
        var scoring = loaded.Board.Configuration.Scoring;
        var points = plays.Skip(start).Take(end - start + 1)
            .Where(p => !p.IsStageBroken && p.Type == type)
            .Sum(p => (int)scoring.GetScorelessScore(charts[p.ChartId]));

        return new MoMImportChecks(
            checks.Charts,
            points,
            checks.SongTime,
            checks.OverWindowBeforeLast,
            checks.Span,
            checks.SpanOverWindow,
            checks.LongestBreak?.Length,
            // Cast, do not let the conditional infer: Name is a struct with an implicit conversion
            // FROM string, so `cond ? null : someName` types itself as Name and converts the null
            // branch through Name.From(null), which throws rather than yielding no name.
            checks.LongestBreak == null ? null : (Name?)charts[checks.LongestBreak.BeforeChartId].Song.Name,
            checks.LongestBreak?.BeforePlayedAt,
            plays.Skip(start).Take(end - start + 1).Where(p => p.IsStageBroken)
                .Select(p => charts[p.ChartId].Song.Name).ToArray(),
            checks.WrongTypeSkipped,
            checks.RepeatPlays);
    }

    private static TimeSpan Song(IReadOnlyList<MoMPlay> plays, int start, int end) =>
        TimeSpan.FromTicks(plays.Skip(start).Take(end - start + 1).Sum(p => p.Duration.Ticks));

    private static TimeSpan Rest(IReadOnlyList<MoMPlay> plays, int start, int end)
    {
        var span = plays[end].EndsAt - plays[start].PlayedAt;
        var rest = span - Song(plays, start, end);
        return rest < TimeSpan.Zero ? TimeSpan.Zero : rest;
    }
}
