using System.Text.RegularExpressions;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Data.Clients;

/// <summary>
///     Swaps the emoji-token vocabulary a Discord message is written in (#LETTERGRADE|…#, #PLATE|…#,
///     #DIFFICULTY|…#, #MIX|…#) for the guild emojis, for the plain and rich send paths alike.
///     <para>
///         A grade or plate token may name an art set ahead of its value
///         (<c>#LETTERGRADE|Rise/SS|False#</c>) and draws that set's emoji; without one it draws the
///         Phoenix set. A difficulty token draws its folder's stepball, so a half-double wears the
///         Doubles one. A token nothing resolves is dropped rather than shown as text.
///     </para>
/// </summary>
public static class DiscordEmojiTokens
{
    private sealed record ArtSet(
        IReadOnlyDictionary<PhoenixLetterGrade, string> Letters,
        IReadOnlyDictionary<PhoenixLetterGrade, string> BrokenLetters,
        IReadOnlyDictionary<PhoenixPlate, string> Plates);

    private static readonly Regex Token = new(@"#(LETTERGRADE|PLATE|DIFFICULTY|MIX)\|([^#\s]*)#",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex TypeAndLevel = new(@"^([A-Za-z]+)(\d+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly IReadOnlyDictionary<PhoenixLetterGrade, string> LetterGradeEmojis =
        new Dictionary<PhoenixLetterGrade, string>
        {
            { PhoenixLetterGrade.F, "<:piu_f:1238540776091422882>" },
            { PhoenixLetterGrade.D, "<:piu_d:1238540632591568948>" },
            { PhoenixLetterGrade.C, "<:piu_c:1238540630632824912>" },
            { PhoenixLetterGrade.B, "<:piu_b:1238540628539867240>" },
            { PhoenixLetterGrade.A, "<:piu_a:1238540428844990524>" },
            { PhoenixLetterGrade.APlus, "<:piu_aplus:1238540626983915580>" },
            { PhoenixLetterGrade.AA, "<:piu_aa:1238540431457910840>" },
            { PhoenixLetterGrade.AAPlus, "<:piu_aaplus:1238540479910641704>" },
            { PhoenixLetterGrade.AAA, "<:piu_aaa:1238540433391484928>" },
            { PhoenixLetterGrade.AAAPlus, "<:piu_aaaplus:1238540436520308746>" },
            { PhoenixLetterGrade.S, "<:piu_s:1238540781573243040>" },
            { PhoenixLetterGrade.SPlus, "<:piu_splus:1238540841719697501>" },
            { PhoenixLetterGrade.SS, "<:piu_ss:1238541129448951848>" },
            { PhoenixLetterGrade.SSPlus, "<:piu_ssplus:1238541131902615585>" },
            { PhoenixLetterGrade.SSS, "<:piu_sss:1238541133982732408>" },
            { PhoenixLetterGrade.SSSPlus, "<:piu_sssplus:1238541135681552435>" }
        };

    private static readonly IReadOnlyDictionary<PhoenixLetterGrade, string> BrokenLetterGradeEmojis =
        new Dictionary<PhoenixLetterGrade, string>
        {
            { PhoenixLetterGrade.F, "<:piu_f_broken:1238540776993198203>" },
            { PhoenixLetterGrade.D, "<:piu_d_broken:1238540672706019420>" },
            { PhoenixLetterGrade.C, "<:piu_c_broken:1238540631534469293>" },
            { PhoenixLetterGrade.B, "<:piu_b_broken:1238540629471006855>" },
            { PhoenixLetterGrade.A, "<:piu_a_broken:1238540429956354159>" },
            { PhoenixLetterGrade.APlus, "<:piu_aplus_broken:1238540627830898758>" },
            { PhoenixLetterGrade.AA, "<:piu_aa_broken:1238540432699559936>" },
            { PhoenixLetterGrade.AAPlus, "<:piu_aaplus_broken:1238540440232394813>" },
            { PhoenixLetterGrade.AAA, "<:piu_aaa_broken:1238540434402447420>" },
            { PhoenixLetterGrade.AAAPlus, "<:piu_aaaplus_broken:1238540437665611837>" },
            { PhoenixLetterGrade.S, "<:piu_s_broken:1238540966109904906>" },
            { PhoenixLetterGrade.SPlus, "<:piu_splus_broken:1238540843334242408>" },
            { PhoenixLetterGrade.SS, "<:piu_ss_broken:1238541131130732564>" },
            { PhoenixLetterGrade.SSPlus, "<:piu_ssplus_broken:1238541132976230402>" },
            { PhoenixLetterGrade.SSS, "<:piu_sss_broken:1238541134758674583>" },
            { PhoenixLetterGrade.SSSPlus, "<:piu_sssplus_broken:1238541136545714196>" }
        };

    private static readonly IReadOnlyDictionary<PhoenixPlate, string> PlateEmojis =
        new Dictionary<PhoenixPlate, string>
        {
            { PhoenixPlate.RoughGame, "<:piu_rg:1238540780402901033>" },
            { PhoenixPlate.FairGame, "<:piu_fg:1238540777890644069>" },
            { PhoenixPlate.TalentedGame, "<:piu_tg:1238541195932598353>" },
            { PhoenixPlate.MarvelousGame, "<:piu_mg:1238540779052470343>" },
            { PhoenixPlate.ExtremeGame, "<:piu_eg:1238540635343159457>" },
            { PhoenixPlate.SuperbGame, "<:piu_sg:1238540784450670713>" },
            { PhoenixPlate.UltimateGame, "<:piu_ug:1238541140429639781>" },
            { PhoenixPlate.PerfectGame, "<:piu_pg:1238540780017025185>" }
        };

    // RISE's nine-grade ladder and its three marks, stored as the Phoenix plates they match:
    // Perfect Game, Full Combo (Ultimate Game) and No Miss (Superb Game).
    private static readonly IReadOnlyDictionary<PhoenixLetterGrade, string> RiseLetterGradeEmojis =
        new Dictionary<PhoenixLetterGrade, string>
        {
            { PhoenixLetterGrade.SSS, "<:rise_sss:1552390855631839383>" },
            { PhoenixLetterGrade.SS, "<:rise_ss:1552390816037863544>" },
            { PhoenixLetterGrade.S, "<:rise_s:1552390774656606218>" },
            { PhoenixLetterGrade.AA, "<:rise_aa:1552390462739054742>" },
            { PhoenixLetterGrade.A, "<:rise_a:1552390419931861112>" },
            { PhoenixLetterGrade.B, "<:rise_b:1552390496893014106>" },
            { PhoenixLetterGrade.C, "<:rise_c:1552390529990402169>" },
            { PhoenixLetterGrade.D, "<:rise_d:1552390569135702116>" },
            { PhoenixLetterGrade.F, "<:rise_f:1552390646013235371>" }
        };

    private static readonly IReadOnlyDictionary<PhoenixLetterGrade, string> RiseBrokenLetterGradeEmojis =
        new Dictionary<PhoenixLetterGrade, string>
        {
            { PhoenixLetterGrade.SSS, "<:rise_sss_broken:1552390874107744266>" },
            { PhoenixLetterGrade.SS, "<:rise_ss_broken:1552390835390390272>" },
            { PhoenixLetterGrade.S, "<:rise_s_broken:1552390793552199791>" },
            { PhoenixLetterGrade.AA, "<:rise_aa_broken:1552390479654555788>" },
            { PhoenixLetterGrade.A, "<:rise_a_broken:1552390442904199268>" },
            { PhoenixLetterGrade.B, "<:rise_b_broken:1552390514060427364>" },
            { PhoenixLetterGrade.C, "<:rise_c_broken:1552390546914549851>" },
            { PhoenixLetterGrade.D, "<:rise_d_broken:1552390621262651453>" },
            { PhoenixLetterGrade.F, "<:rise_f_broken:1552390672571695164>" }
        };

    private static readonly IReadOnlyDictionary<PhoenixPlate, string> RisePlateEmojis =
        new Dictionary<PhoenixPlate, string>
        {
            { PhoenixPlate.PerfectGame, "<:rise_pg:1552390750107340890>" },
            { PhoenixPlate.UltimateGame, "<:rise_fc:1552390700962943036>" },
            { PhoenixPlate.SuperbGame, "<:rise_nm:1552390727974260796>" }
        };

    // Keyed by the set name a token carries; the unnamed set is the Phoenix one.
    private static readonly IReadOnlyDictionary<string, ArtSet> ArtSets =
        new Dictionary<string, ArtSet>(StringComparer.OrdinalIgnoreCase)
        {
            { string.Empty, new ArtSet(LetterGradeEmojis, BrokenLetterGradeEmojis, PlateEmojis) },
            { "Rise", new ArtSet(RiseLetterGradeEmojis, RiseBrokenLetterGradeEmojis, RisePlateEmojis) }
        };

    // Color is the mix signal at inline size: Phoenix = blue, Phoenix 2 = green, XX = pink. Both RISE
    // mixes wear the RISE wordmark.
    private static readonly IReadOnlyDictionary<string, string> MixEmojis =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Phoenix", "<:phoenix_logo:1523325598171398164>" },
            { "Phoenix2", "<:phoenix2_logo:1523325648976875704>" },
            { "XX", "<:xx_logo:1523325684259356703>" },
            { "Rise", "<:rise_logo:1552390373316235364>" },
            { "RiseArcade", "<:rise_logo:1552390373316235364>" }
        };

    // One stepball per folder: Singles, Doubles and the co-op party sizes.
    private static readonly IReadOnlyDictionary<string, string> StepballEmojis =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "s1", "<:s1:1238568406257635402>" },
            { "s2", "<:s2:1238568405158858752>" },
            { "s3", "<:s3:1238568404085113024>" },
            { "s4", "<:s4:1238568403099451422>" },
            { "s5", "<:s5:1238568402197676133>" },
            { "s6", "<:s6:1238568401081733262>" },
            { "s7", "<:s7:1238568342466465832>" },
            { "s8", "<:s8:1238568303841247273>" },
            { "s9", "<:s9:1238568341531131935>" },
            { "s10", "<:s10:1238568301257293914>" },
            { "s11", "<:s11:1238568300280156320>" },
            { "s12", "<:s12:1238568299265134592>" },
            { "s13", "<:s13:1238568298317090897>" },
            { "s14", "<:s14:1238568297138622465>" },
            { "s15", "<:s15:1238568295439925371>" },
            { "s16", "<:s16:1238568296379580529>" },
            { "s17", "<:s17:1238568166498504824>" },
            { "s18", "<:s18:1238568213437223072>" },
            { "s19", "<:s19:1238568163327606814>" },
            { "s20", "<:s20:1238568155702497280>" },
            { "s21", "<:s21:1238568162375766167>" },
            { "s22", "<:s22:1238568161373327440>" },
            { "s23", "<:s23:1238568160060244079>" },
            { "s24", "<:s24:1238568159322050570>" },
            { "s25", "<:s25:1238568158449766531>" },
            { "s26", "<:s26:1238568156855926924>" },
            { "d1", "<:d1:1238582930926997724>" },
            { "d2", "<:d2:1238582928985034904>" },
            { "d3", "<:d3:1238582926292160614>" },
            { "d4", "<:d4:1238582925575065641>" },
            { "d5", "<:d5:1238569292165943296>" },
            { "d6", "<:d6:1238569328798863441>" },
            { "d7", "<:d7:1238569288617558017>" },
            { "d8", "<:d8:1238569287476711535>" },
            { "d9", "<:d9:1238569286331666553>" },
            { "d10", "<:d10:1238569285312315514>" },
            { "d11", "<:d11:1238569284356280360>" },
            { "d12", "<:d12:1238569283471151154>" },
            { "d13", "<:d13:1238569282363854919>" },
            { "d14", "<:d14:1238569281260617750>" },
            { "d15", "<:d15:1238568738165620828>" },
            { "d16", "<:d16:1238568698135056434>" },
            { "d17", "<:d17:1238582924551393371>" },
            { "d18", "<:d18:1238568695618474115>" },
            { "d19", "<:d19:1238568694154526732>" },
            { "d20", "<:d20:1238568693580042250>" },
            { "d21", "<:d21:1238568692099321917>" },
            { "d22", "<:d22:1238568691545673758>" },
            { "d23", "<:d23:1238568690711007292>" },
            { "d24", "<:d24:1238568689888919664>" },
            { "d25", "<:d25:1238568411915882497>" },
            { "d26", "<:d26:1238568456031436871>" },
            { "d27", "<:d27:1238568407813591051>" },
            { "d28", "<:d28:1238568407075655810>" },
            { "d29", "<:d29:1358437131667902617>" },
            { "coop2", "<:coop2:1238582935804842094>" },
            { "coop3", "<:coop3:1238582934185709621>" },
            { "coop4", "<:coop4:1238582932969361478>" },
            { "coop5", "<:coop5:1238582931858001991>" }
        };

    public static string Replace(string message)
    {
        return Token.Replace(message,
            match => Resolve(match.Groups[1].Value, match.Groups[2].Value) ?? string.Empty);
    }

    private static string? Resolve(string kind, string value)
    {
        return kind.ToUpperInvariant() switch
        {
            "LETTERGRADE" => LetterGrade(value),
            "PLATE" => Plate(value),
            "DIFFICULTY" => Difficulty(value),
            "MIX" => MixEmojis.GetValueOrDefault(value),
            _ => null
        };
    }

    private static string? LetterGrade(string value)
    {
        var parts = value.Split('|');
        var (set, name) = SplitSet(parts[0]);
        var isBroken = parts.Length > 1 && bool.TryParse(parts[1], out var broken) && broken;
        if (!ArtSets.TryGetValue(set, out var art) ||
            !Enum.TryParse<PhoenixLetterGrade>(name, true, out var grade)) return null;
        return (isBroken ? art.BrokenLetters : art.Letters).GetValueOrDefault(grade);
    }

    private static string? Plate(string value)
    {
        var (set, name) = SplitSet(value);
        if (!ArtSets.TryGetValue(set, out var art) ||
            !Enum.TryParse<PhoenixPlate>(name, true, out var plate)) return null;
        return art.Plates.GetValueOrDefault(plate);
    }

    // The folder decides the stepball: SP and DP draw S and D, and a half-double draws D.
    private static string? Difficulty(string value)
    {
        var match = TypeAndLevel.Match(value);
        if (!match.Success) return null;
        ChartType type;
        try
        {
            type = ChartTypeHelperMethods.ParseChartTypeShortHand(match.Groups[1].Value);
        }
        catch (ArgumentException)
        {
            return null;
        }

        return StepballEmojis.GetValueOrDefault(type.Category().GetShortHand() + match.Groups[2].Value);
    }

    private static (string Set, string Name) SplitSet(string value)
    {
        var slash = value.IndexOf('/');
        return slash < 0 ? (string.Empty, value) : (value[..slash], value[(slash + 1)..]);
    }
}
