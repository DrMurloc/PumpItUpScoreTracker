using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.OfficialMirror.Infrastructure.Apis.Dtos;

internal sealed class PiuGameGetRecentScoresResult
{
    public Name SongName { get; set; }
    public DifficultyLevel Level { get; set; }
    public ChartType ChartType { get; set; }

    /// <summary>
    ///     NULL on a stage break: the card prints "STAGE BREAK" where the number would be, and
    ///     the game has no chart score for a song it stopped short.
    /// </summary>
    public PhoenixScore? Score { get; set; }

    /// <summary>
    ///     NULL on a broken play — the game awards no plate for a failed stage. Deriving one
    ///     from the judgement counts minted a Perfect Game for every walk-off, whose counts are
    ///     all zero.
    /// </summary>
    public PhoenixPlate? Plate { get; set; }

    /// <summary>
    ///     The grade the site itself printed on the card, or null where the card carried no
    ///     grade art. Read but never stored: a record's LetterGrade is computed from its score,
    ///     so this is the only channel through which the site can contradict our own cutoff
    ///     table rather than us comparing our answer to itself.
    /// </summary>
    public PhoenixLetterGrade? Grade { get; set; }

    public int NoteCount { get; set; }
    public bool IsBroken { get; set; }

    /// <summary>
    ///     The stage broke — the song ended before its last note. Always broken too. The card
    ///     says so with "STAGE BREAK" in the score slot and an empty grade slot; a failed but
    ///     finished stage prints a number and an x_-prefixed grade instead. Its judgement counts
    ///     stop where the stage did (docs/design/stage-breaks-and-max-combo.md).
    /// </summary>
    public bool IsStageBroken { get; set; }

    public int Perfects { get; set; }
    public int Greats { get; set; }
    public int Goods { get; set; }
    public int Bads { get; set; }
    public int Misses { get; set; }

    /// <summary>When the play was saved. Both sites stamp it on recently-played cards.</summary>
    public DateTimeOffset? RecordedAt { get; set; }
}
