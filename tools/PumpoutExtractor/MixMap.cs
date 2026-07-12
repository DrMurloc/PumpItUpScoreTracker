namespace PumpoutExtractor;

/// <summary>
///     Maps pumpout mixes onto the site's Mix rows / MixEnum values.
///     The Guids here MUST stay in lockstep with ScoreTracker.Data's MixIds and the
///     Mix-seeding migration — they were minted once (2026-07-11) and are hardcoded
///     in both places on purpose. Prime JE folds into Prime (owner decision): its
///     versions extend Prime's timeline and it gets no row of its own.
/// </summary>
public static class MixMap
{
    public sealed record MixDef(
        long? PumpoutMixId,
        string EnumName,
        string ShortName,
        string FullName,
        Guid Id,
        int SortOrder,
        bool PreExceedSlots);

    public static readonly Guid XX = Guid.Parse("20f8ccf8-94b1-418d-b923-c375b042bda8");
    public static readonly Guid Phoenix = Guid.Parse("1abb8f5a-bda3-40f0-9ce7-1c4f9f8f1d3b");
    public static readonly Guid Phoenix2 = Guid.Parse("a9b7d3c1-52e8-4f06-9b1a-2f8c33e01948");

    /// <summary>Pumpout mixId of Prime JE — folded into Prime everywhere.</summary>
    public const long PrimeJePumpoutId = 2;

    /// <summary>Pumpout mixId of Infinity — a branch: never wins debut attribution.</summary>
    public const long InfinityPumpoutId = 8;

    public static readonly IReadOnlyList<MixDef> All = new List<MixDef>
    {
        new(32, "FirstDanceFloor", "1st", "The 1st Dance Floor", Guid.Parse("4fdce23c-904c-4538-952f-dda636d1b154"), 10, true),
        new(31, "SecondUltimateRemix", "2nd", "2nd Ultimate Remix", Guid.Parse("6558b48d-9ef2-4a51-bc0e-8a0956469d01"), 20, true),
        new(30, "ThirdObg", "3rd", "3rd O.B.G", Guid.Parse("72a67d8a-dd28-470d-9857-cde789bcafd7"), 30, true),
        new(29, "ObgSeasonEvolution", "OBG SE", "The O.B.G / Season Evolution", Guid.Parse("38d59ecf-f5e0-42a3-9111-796eb398ffeb"), 40, true),
        new(28, "Collection", "Collection", "The Collection", Guid.Parse("34ceb319-84fa-4f2d-a48c-98dc861da3fb"), 50, true),
        new(27, "PerfectCollection", "Perfect", "The Perfect Collection", Guid.Parse("f680d1e5-c4f8-4479-8423-cbf59c1512d6"), 60, true),
        new(26, "Extra", "Extra", "Extra", Guid.Parse("84562821-c87e-4346-b0c1-38a7dfa5637f"), 70, true),
        new(24, "Premiere", "Premiere", "The Premiere", Guid.Parse("fd9a0b6a-f241-47a0-980a-f7cb518a8081"), 80, true),
        new(23, "Prex", "Prex", "The Prex", Guid.Parse("084b06f5-5e8a-47bc-8307-442db8000c5b"), 90, true),
        new(22, "Rebirth", "Rebirth", "The Rebirth", Guid.Parse("ce37a838-2cad-40f4-acc0-a67d6fb97239"), 100, true),
        new(21, "Premiere2", "Premiere 2", "The Premiere 2", Guid.Parse("c995a044-e897-4730-b8e9-599b822bca0d"), 110, true),
        new(20, "Prex2", "Prex 2", "The Prex 2", Guid.Parse("953cc701-4a64-4e4b-bbb3-51c7d66bdae6"), 120, true),
        new(19, "Premiere3", "Premiere 3", "The Premiere 3", Guid.Parse("a409d148-8167-4065-a351-5ec45a863f1a"), 130, true),
        new(18, "Prex3", "Prex 3", "The Prex 3", Guid.Parse("94bd6973-8cec-48d7-aff2-b310b3b0b0fe"), 140, true),
        new(17, "Exceed", "Exceed", "Exceed", Guid.Parse("69d234a7-4141-4a69-ac55-114b7164198d"), 150, false),
        new(16, "Exceed2", "Exceed 2", "Exceed 2", Guid.Parse("4b9842c7-ee1b-4b0e-a370-9a966994236a"), 160, false),
        new(15, "Zero", "Zero", "Zero", Guid.Parse("4a18b364-4b9d-42f3-ae79-222cf1d4ed7b"), 170, false),
        new(14, "Nx", "NX", "NX / New Xenesis", Guid.Parse("07cb82dd-d577-41ea-ba9e-9746061752c1"), 180, false),
        new(null, "Pro", "Pro", "Pump It Up Pro", Guid.Parse("00d66eaf-5408-46f1-a88e-74406891c9d6"), 185, false),
        new(13, "Nx2", "NX2", "NX2 / Next Xenesis", Guid.Parse("df15fb43-5e13-4941-a7ae-d979f8fd6220"), 190, false),
        new(12, "NxAbsolute", "NXA", "NX Absolute", Guid.Parse("d4c22342-f0ea-4f8f-9c5b-be75acc980fa"), 200, false),
        new(null, "Pro2", "Pro 2", "Pump It Up Pro 2", Guid.Parse("745660b3-15db-42d1-ad0c-0ee775503f62"), 205, false),
        new(11, "Fiesta", "Fiesta", "Fiesta", Guid.Parse("178562fc-740f-46c6-b957-0a0381cccfc4"), 210, false),
        new(9, "FiestaEx", "Fiesta EX", "Fiesta EX", Guid.Parse("90c0a1e0-0de6-4d05-a035-533669224482"), 220, false),
        new(7, "Fiesta2", "Fiesta 2", "Fiesta 2", Guid.Parse("e172b206-acf9-4a52-a6fe-cbf56fe15167"), 230, false),
        new(8, "Infinity", "Infinity", "Infinity", Guid.Parse("363b8d21-2dde-4ce0-a54e-2aee2b7280a2"), 235, false),
        new(1, "Prime", "Prime", "Prime", Guid.Parse("d8316882-8d08-4993-b692-d0608392fb02"), 240, false),
        new(33, "Prime2", "Prime 2", "Prime 2", Guid.Parse("00e93a6b-9c39-452f-96b0-1df42dbdd0ac"), 250, false),
        new(34, "XX", "XX", "XX", XX, 260, false)
    };

    public static readonly IReadOnlyDictionary<long, MixDef> ByPumpoutId =
        All.Where(m => m.PumpoutMixId.HasValue).ToDictionary(m => m.PumpoutMixId!.Value);
}
