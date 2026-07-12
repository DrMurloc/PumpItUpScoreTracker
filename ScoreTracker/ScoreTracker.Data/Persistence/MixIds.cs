using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Data.Persistence;

/// <summary>
///     The Mix row IDs, previously copy-pasted as a private MixGuids dictionary
///     in four EF repositories. Persistence-level detail (the rows live in scores.Mix).
///     XX/Phoenix/Phoenix2 predate this class in production; the legacy-mix ids were
///     minted once on 2026-07-11 and are seeded by the LegacyMixCatalog migration —
///     they MUST stay in lockstep with tools/PumpoutExtractor/MixMap.cs, which stamps
///     the same ids into the generated backfill scripts (docs/design/legacy-mixes.md).
/// </summary>
public static class MixIds
{
    public static readonly Guid XX = Guid.Parse("20F8CCF8-94B1-418D-B923-C375B042BDA8");
    public static readonly Guid Phoenix = Guid.Parse("1ABB8F5A-BDA3-40F0-9CE7-1C4F9F8F1D3B");

    // Matched pair with the production seed script ("PIU Phoenix 2 - ChartMix seed.sql") —
    // the scores.Mix row must be inserted with exactly this id.
    public static readonly Guid Phoenix2 = Guid.Parse("A9B7D3C1-52E8-4F06-9B1A-2F8C33E01948");

    private static readonly IReadOnlyDictionary<MixEnum, Guid> ByEnum = new Dictionary<MixEnum, Guid>
    {
        [MixEnum.XX] = XX,
        [MixEnum.Phoenix] = Phoenix,
        [MixEnum.Phoenix2] = Phoenix2,
        [MixEnum.FirstDanceFloor] = Guid.Parse("4FDCE23C-904C-4538-952F-DDA636D1B154"),
        [MixEnum.SecondUltimateRemix] = Guid.Parse("6558B48D-9EF2-4A51-BC0E-8A0956469D01"),
        [MixEnum.ThirdObg] = Guid.Parse("72A67D8A-DD28-470D-9857-CDE789BCAFD7"),
        [MixEnum.ObgSeasonEvolution] = Guid.Parse("38D59ECF-F5E0-42A3-9111-796EB398FFEB"),
        [MixEnum.Collection] = Guid.Parse("34CEB319-84FA-4F2D-A48C-98DC861DA3FB"),
        [MixEnum.PerfectCollection] = Guid.Parse("F680D1E5-C4F8-4479-8423-CBF59C1512D6"),
        [MixEnum.Extra] = Guid.Parse("84562821-C87E-4346-B0C1-38A7DFA5637F"),
        [MixEnum.Premiere] = Guid.Parse("FD9A0B6A-F241-47A0-980A-F7CB518A8081"),
        [MixEnum.Prex] = Guid.Parse("084B06F5-5E8A-47BC-8307-442DB8000C5B"),
        [MixEnum.Rebirth] = Guid.Parse("CE37A838-2CAD-40F4-ACC0-A67D6FB97239"),
        [MixEnum.Premiere2] = Guid.Parse("C995A044-E897-4730-B8E9-599B822BCA0D"),
        [MixEnum.Prex2] = Guid.Parse("953CC701-4A64-4E4B-BBB3-51C7D66BDAE6"),
        [MixEnum.Premiere3] = Guid.Parse("A409D148-8167-4065-A351-5EC45A863F1A"),
        [MixEnum.Prex3] = Guid.Parse("94BD6973-8CEC-48D7-AFF2-B310B3B0B0FE"),
        [MixEnum.Exceed] = Guid.Parse("69D234A7-4141-4A69-AC55-114B7164198D"),
        [MixEnum.Exceed2] = Guid.Parse("4B9842C7-EE1B-4B0E-A370-9A966994236A"),
        [MixEnum.Zero] = Guid.Parse("4A18B364-4B9D-42F3-AE79-222CF1D4ED7B"),
        [MixEnum.Nx] = Guid.Parse("07CB82DD-D577-41EA-BA9E-9746061752C1"),
        [MixEnum.Nx2] = Guid.Parse("DF15FB43-5E13-4941-A7AE-D979F8FD6220"),
        [MixEnum.NxAbsolute] = Guid.Parse("D4C22342-F0EA-4F8F-9C5B-BE75ACC980FA"),
        [MixEnum.Fiesta] = Guid.Parse("178562FC-740F-46C6-B957-0A0381CCCFC4"),
        [MixEnum.FiestaEx] = Guid.Parse("90C0A1E0-0DE6-4D05-A035-533669224482"),
        [MixEnum.Fiesta2] = Guid.Parse("E172B206-ACF9-4A52-A6FE-CBF56FE15167"),
        [MixEnum.Infinity] = Guid.Parse("363B8D21-2DDE-4CE0-A54E-2AEE2B7280A2"),
        [MixEnum.Prime] = Guid.Parse("D8316882-8D08-4993-B692-D0608392FB02"),
        [MixEnum.Prime2] = Guid.Parse("00E93A6B-9C39-452F-96B0-1DF42DBDD0AC"),
        [MixEnum.Pro] = Guid.Parse("00D66EAF-5408-46F1-A88E-74406891C9D6"),
        [MixEnum.Pro2] = Guid.Parse("745660B3-15DB-42D1-AD0C-0EE775503F62")
    };

    private static readonly IReadOnlyDictionary<Guid, MixEnum> ByGuid =
        ByEnum.ToDictionary(kv => kv.Value, kv => kv.Key);

    public static Guid For(MixEnum mix)
    {
        return ByEnum.TryGetValue(mix, out var id)
            ? id
            : throw new ArgumentOutOfRangeException(nameof(mix), mix, "No Mix row id known for mix");
    }

    public static MixEnum ToEnum(Guid mixId)
    {
        return ByGuid.TryGetValue(mixId, out var mix)
            ? mix
            : throw new ArgumentOutOfRangeException(nameof(mixId), mixId, "No MixEnum known for Mix row id");
    }
}
