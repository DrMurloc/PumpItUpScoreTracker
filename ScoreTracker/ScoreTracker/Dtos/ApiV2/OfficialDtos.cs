using ScoreTracker.OfficialMirror.Contracts;

namespace ScoreTracker.Web.Dtos.ApiV2;

/// <summary>
///     A player as the official boards show them.
///     <para>
///         <b>Carries no PIU Scores user id, deliberately.</b> <see cref="OfficialPlayerRecord" />
///         holds one internally when an import has linked the accounts, and returning it here would
///         let any caller map a piugame tag to a site account with no share — including for players
///         whose profile is private. The piugame side is public; the link is not. A caller who needs
///         the join has <c>gameTag</c> on both sides and can do it deliberately.
///     </para>
/// </summary>
public sealed class OfficialPlayerDto
{
    public OfficialPlayerDto(OfficialPlayerRecord record)
    {
        PlayerId = record.PlayerId;
        GameTag = record.Username;
        AvatarUrl = record.AvatarUrl?.ToString();
        IsSupplemented = record.IsSupplemented;
    }

    /// <summary>piugame's own player id, which is public on their boards.</summary>
    public int PlayerId { get; set; }

    /// <summary>The in-game tag as piugame spells it, discriminator included.</summary>
    public string GameTag { get; set; }

    /// <summary>The avatar piugame shows on its boards, when it has one.</summary>
    public string? AvatarUrl { get; set; }

    /// <summary>
    ///     True when this player is on the board only because PIU Scores knows their scores — a
    ///     public PIU Scores account whose verified bests were folded in by
    ///     <c>supplemented=true</c>. A property of the reading, not the person: always false in the
    ///     official reading.
    /// </summary>
    public bool IsSupplemented { get; set; }
}

/// <summary>One row of the PUMBILITY ranking.</summary>
public sealed class OfficialRankingDto
{
    /// <summary>The player's place this week.</summary>
    public int Rank { get; set; }

    /// <summary>
    ///     The player's place last week. A player who was not on last week's ranking counts as one
    ///     place below its last row, so the climb is real; null only when there was no last week.
    /// </summary>
    public int? PreviousRank { get; set; }

    public OfficialPlayerDto Player { get; set; } = null!;

    /// <summary>The PUMBILITY the ranking is ordered on. See <see cref="OfficialRankingsDto.RatingIsOfficial" /> for whose number it is.</summary>
    public decimal Rating { get; set; }

    /// <summary>How many chart boards the player is in the top 300 of this week.</summary>
    public int BoardsInTop { get; set; }

    /// <summary>The site's read of the player's style from their best fifty: PassPusher, PassRefiner, BalancedPlayer, Competitive or Perfectionist; null when it has none.</summary>
    public string? PlayerType { get; set; }
}

/// <summary>The PUMBILITY ranking at the latest weekly snapshot.</summary>
public sealed class OfficialRankingsDto
{
    /// <summary>When the weekly snapshot this ranking comes from was taken; null when the mix has none yet.</summary>
    public DateTimeOffset? SnapshotAt { get; set; }

    /// <summary>
    ///     False when the rating is our own computation rather than a number piugame publishes —
    ///     the CO-OP board is estimated, for instance.
    /// </summary>
    public bool RatingIsOfficial { get; set; }

    /// <summary>The ranking, first place first.</summary>
    public OfficialRankingDto[] Data { get; set; } = Array.Empty<OfficialRankingDto>();
}

/// <summary>One of a player's chart-board placements.</summary>
public sealed class OfficialPlacementDto
{
    /// <summary>The chart, as <c>/api/v2/charts</c> ids it.</summary>
    public Guid ChartId { get; set; }

    /// <summary>The player's place on the chart's board this week.</summary>
    public int? Place { get; set; }

    /// <summary>Places gained since last week — positive is up. Null when the player was not on the board last week.</summary>
    public int? PlaceDelta { get; set; }

    /// <summary>The score the board lists for the player.</summary>
    public int Score { get; set; }

    /// <summary>
    ///     What this placement is worth in PUMBILITY. A double rather than an int: the value has
    ///     a real fraction, and <see cref="OfficialPlayerProfileDto.Pumbility" /> on the same
    ///     payload has always carried one.
    /// </summary>
    public double ComputedRating { get; set; }

    /// <summary>
    ///     True when this placement is a verified PIU Scores best the official board does not list,
    ///     appended below the board's official rows by <c>supplemented=true</c>. Always false in the
    ///     official reading.
    /// </summary>
    public bool IsSupplemented { get; set; }
}

/// <summary>One week of a player's history on the boards.</summary>
public sealed class OfficialHistoryPointDto
{
    /// <summary>When that week's snapshot was taken.</summary>
    public DateTimeOffset At { get; set; }

    /// <summary>The player's PUMBILITY that week; null when they were not on the ranking.</summary>
    public decimal? Pumbility { get; set; }

    /// <summary>The player's place on the PUMBILITY ranking that week; null when they were not on it.</summary>
    public int? PumbilityRank { get; set; }

    /// <summary>How many chart boards the player was in the top 300 of that week.</summary>
    public int BoardsInTop { get; set; }
}

public sealed class OfficialPlayerProfileDto
{
    public OfficialPlayerProfileDto(OfficialPlayerProfileRecord record)
    {
        Player = new OfficialPlayerDto(record.Player);
        PlayerType = record.PlayerType?.ToString();
        Pumbility = record.Pumbility;
        PumbilityIsSupplemented = record.PumbilityIsSupplemented;
        PumbilityRank = record.PumbilityRank;
        PumbilityRankDelta = record.PumbilityRankDelta;
        BoardsInTop = record.BoardsInTop;
        NumberOnes = record.NumberOnes;
        BestPlace = record.BestPlace;
        TopTens = record.TopTens;
        History = record.History.Select(h => new OfficialHistoryPointDto
        {
            At = h.At, Pumbility = h.Pumbility, PumbilityRank = h.PumbilityRank, BoardsInTop = h.BoardsInTop
        }).ToArray();
        Placements = record.Placements.Select(p => new OfficialPlacementDto
        {
            ChartId = p.ChartId, Place = p.Place, PlaceDelta = p.PlaceDelta,
            Score = p.Score, ComputedRating = p.ComputedRating, IsSupplemented = p.IsSupplemented
        }).ToArray();
    }

    public OfficialPlayerDto Player { get; set; }

    /// <summary>The site's read of the player's style from their best fifty: PassPusher, PassRefiner, BalancedPlayer, Competitive or Perfectionist; null when it has none.</summary>
    public string? PlayerType { get; set; }

    /// <summary>The player's PUMBILITY at the latest snapshot; null when they are not on the ranking in this reading.</summary>
    public decimal? Pumbility { get; set; }

    /// <summary>
    ///     True when <see cref="Pumbility" /> is PIU Scores' computed number for a player piugame's
    ///     ranking does not list, supplied by <c>supplemented=true</c>, rather than piugame's own.
    ///     False in the official reading, and false when there is no PUMBILITY value at all.
    /// </summary>
    public bool PumbilityIsSupplemented { get; set; }

    /// <summary>The player's place on the PUMBILITY ranking; null when they are not on it in this reading.</summary>
    public int? PumbilityRank { get; set; }

    /// <summary>Places gained on the ranking since last week — positive is up. Null when either week has no place for them.</summary>
    public int? PumbilityRankDelta { get; set; }

    /// <summary>How many chart boards the player is in the top 300 of this week.</summary>
    public int BoardsInTop { get; set; }

    /// <summary>How many chart boards the player leads.</summary>
    public int NumberOnes { get; set; }

    /// <summary>The player's best place on any chart board; 0 when they are on none.</summary>
    public int BestPlace { get; set; }

    /// <summary>How many chart boards the player is in the top ten of.</summary>
    public int TopTens { get; set; }

    /// <summary>Week by week, oldest first.</summary>
    public OfficialHistoryPointDto[] History { get; set; }

    /// <summary>Every chart board the player is on this week, best place first.</summary>
    public OfficialPlacementDto[] Placements { get; set; }
}

/// <summary>One row of a chart's board.</summary>
public sealed class OfficialBoardEntryDto
{
    /// <summary>The place on the board.</summary>
    public int Place { get; set; }

    public OfficialPlayerDto Player { get; set; } = null!;

    /// <summary>The score the board lists.</summary>
    public int Score { get; set; }
}

/// <summary>A chart's board at the latest weekly snapshot.</summary>
public sealed class OfficialChartBoardDto
{
    /// <summary>When the weekly snapshot this board comes from was taken.</summary>
    public DateTimeOffset AsOf { get; set; }

    /// <summary>The board, first place first.</summary>
    public OfficialBoardEntryDto[] Data { get; set; } = Array.Empty<OfficialBoardEntryDto>();
}

/// <summary>Where a chart stands in piugame's play-count ranking.</summary>
public sealed class OfficialPopularityDto
{
    /// <summary>The chart, as <c>/api/v2/charts</c> ids it.</summary>
    public Guid ChartId { get; set; }

    /// <summary>The chart's place in the popularity ranking this week — 1 is the most played.</summary>
    public int Place { get; set; }

    /// <summary>The chart's place last week; null when it was not ranked then.</summary>
    public int? PreviousPlace { get; set; }

    /// <summary>Most recent last. Length is the requested trend window.</summary>
    public int[] RecentPlaces { get; set; } = Array.Empty<int>();
}

/// <summary>
///     The PUMBILITY it takes to hold one rank, and the uniform ladder that clears it: the level at
///     which fifty charts all at one grade would add up to that number.
/// </summary>
public sealed class CutlineTierDto
{
    /// <summary>The rank this cutline is for — for example 100 means the hundredth place.</summary>
    public int Rank { get; set; }

    /// <summary>The PUMBILITY held by the player at that rank this week.</summary>
    public decimal Value { get; set; }

    /// <summary>How much that number moved since last week; null when there was no last week.</summary>
    public decimal? WeekDelta { get; set; }

    /// <summary>The level at which fifty AAA scores would clear this cutline; null when no level does.</summary>
    public int? LevelForAAA { get; set; }

    /// <summary>The level at which fifty S scores would clear this cutline; null when no level does.</summary>
    public int? LevelForS { get; set; }

    /// <summary>The level at which fifty SS scores would clear this cutline; null when no level does.</summary>
    public int? LevelForSS { get; set; }

    /// <summary>The level at which fifty SSS scores would clear this cutline; null when no level does.</summary>
    public int? LevelForSSS { get; set; }
}

/// <summary>What it takes to get onto one of the PUMBILITY boards at all.</summary>
public sealed class BoardCutlineDto
{
    /// <summary>Which PUMBILITY board the cutline is for.</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>The PUMBILITY of the board's last row — the number to beat to get on; null when the board is not mirrored.</summary>
    public decimal? EntryValue { get; set; }

    /// <summary>How much the entry number moved since last week; null when there was no last week.</summary>
    public decimal? WeekDelta { get; set; }

    /// <summary>True when the board has every row piugame publishes filled, so the entry number is a real cut.</summary>
    public bool BoardFull { get; set; }
}

/// <summary>One week of the entry cutline's history.</summary>
public sealed class CutlineHistoryPointDto
{
    /// <summary>When that week's snapshot was taken.</summary>
    public DateTimeOffset At { get; set; }

    /// <summary>The entry cutline that week.</summary>
    public decimal Value { get; set; }

    /// <summary>The level at which fifty AAA scores cleared it that week; null when no level did.</summary>
    public int? LevelForAAA { get; set; }

    /// <summary>The level at which fifty S scores cleared it that week; null when no level did.</summary>
    public int? LevelForS { get; set; }

    /// <summary>The level at which fifty SS scores cleared it that week; null when no level did.</summary>
    public int? LevelForSS { get; set; }

    /// <summary>The level at which fifty SSS scores cleared it that week; null when no level did.</summary>
    public int? LevelForSSS { get; set; }
}

public sealed class WhatItTakesDto
{
    public WhatItTakesDto(WhatItTakesRecord record)
    {
        SnapshotAt = record.SnapshotAt;
        BoardFull = record.BoardFull;
        BoardCount = record.BoardCount;
        Entry = record.Entry is null ? null : Tier(record.Entry);
        Tiers = record.Tiers.Select(Tier).ToArray();
        Boards = record.Boards.Select(b => new BoardCutlineDto
        {
            Type = b.Type, EntryValue = b.EntryValue, WeekDelta = b.WeekDelta, BoardFull = b.BoardFull
        }).ToArray();
        History = record.History.Select(h => new CutlineHistoryPointDto
        {
            At = h.At, Value = h.Value, LevelForAAA = h.LevelForAAA, LevelForS = h.LevelForS,
            LevelForSS = h.LevelForSS, LevelForSSS = h.LevelForSSS
        }).ToArray();
    }

    /// <summary>When the weekly snapshot these cutlines come from was taken; null when the mix has none yet.</summary>
    public DateTimeOffset? SnapshotAt { get; set; }

    /// <summary>True when the PUMBILITY ranking has every row piugame publishes filled, so the entry cutline is a real cut.</summary>
    public bool BoardFull { get; set; }

    /// <summary>How many rows the PUMBILITY ranking holds this week.</summary>
    public int BoardCount { get; set; }

    /// <summary>
    ///     The last row of a full ranking — what it takes to get on at all. Null until the ranking is
    ///     full: piugame caps it at 1,000 places, and until then any top fifty gets on.
    /// </summary>
    public CutlineTierDto? Entry { get; set; }

    /// <summary>The landmark ranks the ranking actually reaches, and what each takes.</summary>
    public CutlineTierDto[] Tiers { get; set; }

    /// <summary>The entry cutline of each PUMBILITY board.</summary>
    public BoardCutlineDto[] Boards { get; set; }

    /// <summary>The entry cutline week by week, oldest first.</summary>
    public CutlineHistoryPointDto[] History { get; set; }

    private static CutlineTierDto Tier(CutlineTierRecord record)
    {
        return new CutlineTierDto
        {
            Rank = record.Rank, Value = record.Value, WeekDelta = record.WeekDelta,
            LevelForAAA = record.LevelForAAA, LevelForS = record.LevelForS,
            LevelForSS = record.LevelForSS, LevelForSSS = record.LevelForSSS
        };
    }
}

public sealed class WeeklyHighlightsDto
{
    public WeeklyHighlightsDto(WeeklyHighlightsRecord record)
    {
        SnapshotAt = record.SnapshotAt;
        PreviousSnapshotAt = record.PreviousSnapshotAt;
        Pulse = record.Pulse is null
            ? null
            : new WeeklyPulseDto
            {
                NewEntries = record.Pulse.NewEntries,
                UpscoredEntries = record.Pulse.UpscoredEntries,
                PlayersActive = record.Pulse.PlayersActive,
                DebutCount = record.Pulse.DebutCount
            };
        Movers = record.Movers.Select(m => new OfficialMoverDto
        {
            Player = new OfficialPlayerDto(m.Player), PreviousRank = m.PreviousRank,
            NewRank = m.NewRank, Pumbility = m.Pumbility
        }).ToArray();
        Gainers = (record.Gainers ?? Array.Empty<OfficialGainerRecord>()).Select(g => new OfficialGainerDto
        {
            Player = new OfficialPlayerDto(g.Player), PreviousPumbility = g.PreviousPumbility,
            NewPumbility = g.NewPumbility, PreviousRank = g.PreviousRank, NewRank = g.NewRank
        }).ToArray();
        Debuts = (record.Debuts ?? Array.Empty<OfficialDebutRecord>()).Select(d => new OfficialDebutDto
        {
            Player = new OfficialPlayerDto(d.Player), BestPlace = d.BestPlace
        }).ToArray();
        BoardsClimbed = record.BoardsClimbed.Select(b => new OfficialBoardsClimbedDto
        {
            Player = new OfficialPlayerDto(b.Player), BoardsClimbed = b.BoardsClimbed,
            NetPlacesGained = b.NetPlacesGained, NewBoards = b.NewBoards
        }).ToArray();
        WorldFirsts = record.WorldFirsts.Select(w => new OfficialWorldFirstDto
        {
            Player = new OfficialPlayerDto(w.Player), ChartId = w.ChartId, ChartType = w.ChartType,
            Level = w.Level, GradeBand = w.GradeBand, Score = w.Score, IsFolderFirst = w.IsFolderFirst
        }).ToArray();
        NewNumberOnes = record.NewNumberOnes.Select(n => new OfficialNewNumberOneDto
        {
            Player = new OfficialPlayerDto(n.Player), ChartId = n.ChartId, Score = n.Score,
            Dethroned = n.Dethroned is null ? null : new OfficialPlayerDto(n.Dethroned)
        }).ToArray();
        Floors = (record.Floors ?? Array.Empty<OfficialFloorMarkRecord>()).Select(f => new OfficialFloorMarkDto
        {
            Rank = f.Rank, Value = f.Value, PreviousValue = f.PreviousValue,
            LevelForSS = f.LevelForSS, PreviousLevelForSS = f.PreviousLevelForSS
        }).ToArray();
    }

    /// <summary>When this week's snapshot was taken.</summary>
    public DateTimeOffset SnapshotAt { get; set; }

    /// <summary>When last week's snapshot was taken — the week everything below is measured against; null on a mix's first week.</summary>
    public DateTimeOffset? PreviousSnapshotAt { get; set; }

    /// <summary>The week's activity in four numbers; null when this is the mix's first week.</summary>
    public WeeklyPulseDto? Pulse { get; set; }

    /// <summary>The players who climbed the PUMBILITY ranking the most this week.</summary>
    public OfficialMoverDto[] Movers { get; set; }

    /// <summary>The players whose PUMBILITY grew the most this week.</summary>
    public OfficialGainerDto[] Gainers { get; set; }

    /// <summary>A sample of the players who appeared on a board for the first time this week.</summary>
    public OfficialDebutDto[] Debuts { get; set; }

    /// <summary>The players who climbed the most chart boards this week.</summary>
    public OfficialBoardsClimbedDto[] BoardsClimbed { get; set; }

    /// <summary>The first time anyone reached a grade on a chart, or on a whole folder. Always from the official boards.</summary>
    public OfficialWorldFirstDto[] WorldFirsts { get; set; }

    /// <summary>Chart boards that changed hands at the top this week. Always from the official boards.</summary>
    public OfficialNewNumberOneDto[] NewNumberOnes { get; set; }

    /// <summary>Landmark ranks whose PUMBILITY floor moved this week.</summary>
    public OfficialFloorMarkDto[] Floors { get; set; }
}

/// <summary>The week's activity on the chart boards, in four numbers.</summary>
public sealed class WeeklyPulseDto
{
    /// <summary>Board rows held by a player who was not on that board last week.</summary>
    public int NewEntries { get; set; }

    /// <summary>Board rows whose score rose since last week.</summary>
    public int UpscoredEntries { get; set; }

    /// <summary>How many distinct players are behind those rows.</summary>
    public int PlayersActive { get; set; }

    /// <summary>How many players appeared on any board for the first time. <c>debuts</c> is a sample of them.</summary>
    public int DebutCount { get; set; }
}

/// <summary>A player who climbed the PUMBILITY ranking this week.</summary>
public sealed class OfficialMoverDto
{
    public OfficialPlayerDto Player { get; set; } = null!;

    /// <summary>Their place last week. A player who was not on the ranking counts as one place below its last row.</summary>
    public int PreviousRank { get; set; }

    /// <summary>Their place this week.</summary>
    public int NewRank { get; set; }

    /// <summary>Their PUMBILITY this week.</summary>
    public decimal Pumbility { get; set; }
}

/// <summary>A player whose PUMBILITY grew this week.</summary>
public sealed class OfficialGainerDto
{
    public OfficialPlayerDto Player { get; set; } = null!;

    /// <summary>Their PUMBILITY last week.</summary>
    public decimal PreviousPumbility { get; set; }

    /// <summary>Their PUMBILITY this week.</summary>
    public decimal NewPumbility { get; set; }

    /// <summary>Their place last week.</summary>
    public int PreviousRank { get; set; }

    /// <summary>Their place this week.</summary>
    public int NewRank { get; set; }
}

/// <summary>A player who appeared on a board for the first time this week.</summary>
public sealed class OfficialDebutDto
{
    public OfficialPlayerDto Player { get; set; } = null!;

    /// <summary>The best place they landed on any board.</summary>
    public int BestPlace { get; set; }
}

/// <summary>A player who climbed chart boards this week.</summary>
public sealed class OfficialBoardsClimbedDto
{
    public OfficialPlayerDto Player { get; set; } = null!;

    /// <summary>How many chart boards they moved up on, entries included.</summary>
    public int BoardsClimbed { get; set; }

    /// <summary>Places gained across all of them, net of any lost.</summary>
    public int NetPlacesGained { get; set; }

    /// <summary>How many of those boards they were not on last week; null on weeks recorded before the split existed.</summary>
    public int? NewBoards { get; set; }
}

/// <summary>The first time anyone reached a grade on a chart, or across a whole folder.</summary>
public sealed class OfficialWorldFirstDto
{
    public OfficialPlayerDto Player { get; set; } = null!;

    /// <summary>The chart, for a chart first; null for a folder first.</summary>
    public Guid? ChartId { get; set; }

    /// <summary>The folder's chart type, for a folder first; null for a chart first.</summary>
    public string? ChartType { get; set; }

    /// <summary>The folder's level, for a folder first; null for a chart first.</summary>
    public int? Level { get; set; }

    /// <summary>The grade band reached for the first time — for example SSS or SSS+.</summary>
    public string GradeBand { get; set; } = string.Empty;

    /// <summary>The score that reached it.</summary>
    public int Score { get; set; }

    /// <summary>True when the first is across a whole folder — every chart of one level and type — rather than one chart.</summary>
    public bool IsFolderFirst { get; set; }
}

/// <summary>A chart board that changed hands at the top this week.</summary>
public sealed class OfficialNewNumberOneDto
{
    public OfficialPlayerDto Player { get; set; } = null!;

    /// <summary>The chart whose board changed hands.</summary>
    public Guid ChartId { get; set; }

    /// <summary>The score that took first place.</summary>
    public int Score { get; set; }

    /// <summary>Who held first place last week; null when the board had no leader then.</summary>
    public OfficialPlayerDto? Dethroned { get; set; }
}

/// <summary>A landmark rank whose PUMBILITY floor moved this week.</summary>
public sealed class OfficialFloorMarkDto
{
    /// <summary>The rank — for example 100 or 1000.</summary>
    public int Rank { get; set; }

    /// <summary>The PUMBILITY held at that rank this week.</summary>
    public decimal Value { get; set; }

    /// <summary>The PUMBILITY held at that rank last week; null when there was no last week.</summary>
    public decimal? PreviousValue { get; set; }

    /// <summary>The level at which fifty SS scores would clear this week's floor; null when no level does.</summary>
    public int? LevelForSS { get; set; }

    /// <summary>The same for last week's floor.</summary>
    public int? PreviousLevelForSS { get; set; }
}
