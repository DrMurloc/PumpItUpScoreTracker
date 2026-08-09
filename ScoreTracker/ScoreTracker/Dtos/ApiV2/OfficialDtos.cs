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
    }

    /// <summary>piugame's own player id, which is public on their boards.</summary>
    public int PlayerId { get; set; }

    public string GameTag { get; set; }
    public string? AvatarUrl { get; set; }
}

public sealed class OfficialRankingDto
{
    public int Rank { get; set; }
    public int? PreviousRank { get; set; }
    public OfficialPlayerDto Player { get; set; } = null!;
    public decimal Rating { get; set; }
    public int BoardsInTop { get; set; }
    public string? PlayerType { get; set; }
}

public sealed class OfficialRankingsDto
{
    public DateTimeOffset? SnapshotAt { get; set; }

    /// <summary>
    ///     False when the rating is our own computation rather than a number piugame publishes —
    ///     the CO-OP board is estimated, for instance.
    /// </summary>
    public bool RatingIsOfficial { get; set; }

    public OfficialRankingDto[] Data { get; set; } = Array.Empty<OfficialRankingDto>();
}

public sealed class OfficialPlacementDto
{
    public Guid ChartId { get; set; }
    public int? Place { get; set; }
    public int? PlaceDelta { get; set; }
    public int Score { get; set; }

    /// <summary>
    ///     What this placement is worth in PUMBILITY. A double rather than an int: the value has
    ///     a real fraction, and <see cref="OfficialPlayerProfileDto.Pumbility" /> on the same
    ///     payload has always carried one.
    /// </summary>
    public double ComputedRating { get; set; }
}

public sealed class OfficialHistoryPointDto
{
    public DateTimeOffset At { get; set; }
    public decimal? Pumbility { get; set; }
    public int? PumbilityRank { get; set; }
    public int BoardsInTop { get; set; }
}

public sealed class OfficialPlayerProfileDto
{
    public OfficialPlayerProfileDto(OfficialPlayerProfileRecord record)
    {
        Player = new OfficialPlayerDto(record.Player);
        PlayerType = record.PlayerType?.ToString();
        Pumbility = record.Pumbility;
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
            Score = p.Score, ComputedRating = p.ComputedRating
        }).ToArray();
    }

    public OfficialPlayerDto Player { get; set; }
    public string? PlayerType { get; set; }
    public decimal? Pumbility { get; set; }
    public int? PumbilityRank { get; set; }
    public int? PumbilityRankDelta { get; set; }
    public int BoardsInTop { get; set; }
    public int NumberOnes { get; set; }
    public int BestPlace { get; set; }
    public int TopTens { get; set; }
    public OfficialHistoryPointDto[] History { get; set; }
    public OfficialPlacementDto[] Placements { get; set; }
}

public sealed class OfficialBoardEntryDto
{
    public int Place { get; set; }
    public OfficialPlayerDto Player { get; set; } = null!;
    public int Score { get; set; }
}

public sealed class OfficialChartBoardDto
{
    public DateTimeOffset AsOf { get; set; }
    public OfficialBoardEntryDto[] Data { get; set; } = Array.Empty<OfficialBoardEntryDto>();
}

public sealed class OfficialPopularityDto
{
    public Guid ChartId { get; set; }
    public int Place { get; set; }
    public int? PreviousPlace { get; set; }

    /// <summary>Most recent last. Length is the requested trend window.</summary>
    public int[] RecentPlaces { get; set; } = Array.Empty<int>();
}

public sealed class CutlineTierDto
{
    public int Rank { get; set; }
    public decimal Value { get; set; }
    public decimal? WeekDelta { get; set; }
    public int? LevelForAAA { get; set; }
    public int? LevelForS { get; set; }
    public int? LevelForSS { get; set; }
    public int? LevelForSSS { get; set; }
}

public sealed class BoardCutlineDto
{
    public string Type { get; set; } = string.Empty;
    public decimal? EntryValue { get; set; }
    public decimal? WeekDelta { get; set; }
    public bool BoardFull { get; set; }
}

public sealed class CutlineHistoryPointDto
{
    public DateTimeOffset At { get; set; }
    public decimal Value { get; set; }
    public int? LevelForAAA { get; set; }
    public int? LevelForS { get; set; }
    public int? LevelForSS { get; set; }
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

    public DateTimeOffset? SnapshotAt { get; set; }
    public bool BoardFull { get; set; }
    public int BoardCount { get; set; }
    public CutlineTierDto? Entry { get; set; }
    public CutlineTierDto[] Tiers { get; set; }
    public BoardCutlineDto[] Boards { get; set; }
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

    public DateTimeOffset SnapshotAt { get; set; }
    public DateTimeOffset? PreviousSnapshotAt { get; set; }
    public WeeklyPulseDto? Pulse { get; set; }
    public OfficialMoverDto[] Movers { get; set; }
    public OfficialGainerDto[] Gainers { get; set; }
    public OfficialDebutDto[] Debuts { get; set; }
    public OfficialBoardsClimbedDto[] BoardsClimbed { get; set; }
    public OfficialWorldFirstDto[] WorldFirsts { get; set; }
    public OfficialNewNumberOneDto[] NewNumberOnes { get; set; }
    public OfficialFloorMarkDto[] Floors { get; set; }
}

public sealed class WeeklyPulseDto
{
    public int NewEntries { get; set; }
    public int UpscoredEntries { get; set; }
    public int PlayersActive { get; set; }
    public int DebutCount { get; set; }
}

public sealed class OfficialMoverDto
{
    public OfficialPlayerDto Player { get; set; } = null!;
    public int PreviousRank { get; set; }
    public int NewRank { get; set; }
    public decimal Pumbility { get; set; }
}

public sealed class OfficialGainerDto
{
    public OfficialPlayerDto Player { get; set; } = null!;
    public decimal PreviousPumbility { get; set; }
    public decimal NewPumbility { get; set; }
    public int PreviousRank { get; set; }
    public int NewRank { get; set; }
}

public sealed class OfficialDebutDto
{
    public OfficialPlayerDto Player { get; set; } = null!;
    public int BestPlace { get; set; }
}

public sealed class OfficialBoardsClimbedDto
{
    public OfficialPlayerDto Player { get; set; } = null!;
    public int BoardsClimbed { get; set; }
    public int NetPlacesGained { get; set; }
    public int? NewBoards { get; set; }
}

public sealed class OfficialWorldFirstDto
{
    public OfficialPlayerDto Player { get; set; } = null!;
    public Guid? ChartId { get; set; }
    public string? ChartType { get; set; }
    public int? Level { get; set; }
    public string GradeBand { get; set; } = string.Empty;
    public int Score { get; set; }
    public bool IsFolderFirst { get; set; }
}

public sealed class OfficialNewNumberOneDto
{
    public OfficialPlayerDto Player { get; set; } = null!;
    public Guid ChartId { get; set; }
    public int Score { get; set; }
    public OfficialPlayerDto? Dethroned { get; set; }
}

public sealed class OfficialFloorMarkDto
{
    public int Rank { get; set; }
    public decimal Value { get; set; }
    public decimal? PreviousValue { get; set; }
    public int? LevelForSS { get; set; }
    public int? PreviousLevelForSS { get; set; }
}
