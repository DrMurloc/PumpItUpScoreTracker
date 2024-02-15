using System.Collections.Immutable;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using CsvHelper;
using Microsoft.AspNetCore.Components.Forms;
using OfficeOpenXml;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.ValueTypes;
using ScoreTracker.Web.Dtos;

namespace ScoreTracker.Web.Services;

public sealed class XXScoreFile
{
    public const int MaxByteCount = 10000000;

    public static readonly IDictionary<string, Name> NameMappings = new Dictionary<string, Name>
    {
        { "8 6", "86" },
        { "%X", "%X (Percent X)" },
        { "%X (Percent  X)", "%X (Percent X)" },
        {
            "2006 Love Song", "2006. LOVE SONG"
        },
        {
            "2006. LOVE SONG", "2006. LOVE SONG"
        },
        { "Adrenaline Blast", "Adrenaline Blaster" },
        {
            "Ai Yurete...", "Ai, Yurete..."
        },
        {
            "Ai, Yurete…", "Ai, Yurete..."
        },
        {
            "Allegro Con Fuocco", "Allegro Con Fuoco"
        },
        {
            "Allegro Fuocco", "Allegro Con Fuoco"
        },
        {
            "Allegro Piu Mosson", "Allegro Piu Mosso"
        },
        {
            "Amphytrion", "Amphitryon"
        },
        {
            "Annhialtor Method", "Annihilator Method"
        },
        {
            "Aschlouias", "Achluoias"
        },
        {
            "Asterios", "Asterios -ReEntry-"
        },
        {
            "Asterios - ReEntry -", "Asterios -ReEntry-"
        },
        {
            "Asteroid", "Asterios -ReEntry-"
        },
        {
            "B.P. Classic Remix", "B.P Classic Remix"
        },
        {
            "B.P. Classic Remix 2 Remix", "B.P Classic Remix 2"
        },
        {
            "B.P Classic Remix 2 Remix", "B.P Classic Remix 2"
        },
        {
            "B.P. Classic Remix Remix", "B.P Classic Remix"
        },
        {
            "Bad Apple", "Bad Apple!! feat. Nomico"
        },
        { "Bad Apple Full Song", "Bad Apple!! feat. Nomico Full Song" },
        {
            "Bad Apple!!", "Bad Apple!! feat. Nomico"
        },
        {
            "Bad Apple!! Full Song", "Bad Apple!! feat. Nomico Full Song"
        },
        {
            "Bad End Night", "Bad 8 End 8 Night"
        },
        {
            "Bad Karma", "Broken Karma (PIU Edit)"
        },
        {
            "Banya Classic Remix Remix", "Banya Classic Remix"
        },
        {
            "BANYA HIPHOP REMIX Remix", "BANYA HIPHOP REMIX"
        },
        {
            "Banya-P Classic Remix Remix", "Banya-P Classic Remix"
        },
        {
            "Banya-P Guitar Mix Remix", "Banya-P Guitar Remix"
        },
        {
            "Bethoven Virus", "Beethoven Virus"
        },
        {
            "Betrayer 2", "Betrayer -act.2-"
        },
        {
            "Betrayer -act 2-", "Betrayer -act.2-"
        },
        { "Blaze Emotions Instrumental", "Blaze emotion (Band version)" },
        { "Blaze Emotion Instrumental", "Blaze emotion (Band version)" },
        { "Blaze emotion(Band version)", "Blaze emotion (Band version)" },
        { "Bon Bon Chocolate", "Bon Bon Chocolat" },
        { "Boong Boong (feat. Sik-K) (prod. GroovyRoom)", "Boong Boong " },
        { "Brainpower", "BRAIN POWER" },
        { "Break It Down!", "Break it Down" },
        { "Breakout", "Break Out" },
        { "Broken Karma", "Broken Karma (PIU Edit)" },
        { "Bungee", "BUNGEE (Fall in Love)" },
        { "Caprise of Otada", "Caprice of Otada" },
        { "Canon X.1", "Cannon X.1" },
        { "Canon-D", "Canon D" },
        { "Carmen", "Carmen Bus" },
        { "Close Your Eyes", "Close Your Eye" },
        { "Creed", "Creed - 1st Desire -" },
        { "Creed Full Song", "Creed - 1st Desire - Full Song" },
        { "CROSS OVER feat. LyuU", "Cross Over" },
        { "Cycling", "Cycling!" },
        { "Dange & Danger", "Danger & Danger" },
        { "Dement ~After Legend~", "Dement" },
        { "Did you know that", "Do U Know That-Old School" },
        { "Do U Know That - Old School", "Do U Know That-Old School" },
        { "Do U Know That-Old School", "Do U Know That-Old School" },
        { "Do it reggae", "Do It Reggae Style" },
        { "Dr.KOA Remix", "Dr. KOA Remix" },
        { "Dual Racing - RED vs BLUE -", "Dual Racing <RED vs BLUE>" },
        { "F(R)IEND", "Friend" },
        { "FAEP 2-1", "Final Audition Ep. 2-1" },
        { "FAEP 2-2", "Final Audition Ep. 2-2" },
        { "Final Audition 2-1", "Final Audition Ep. 2-1" },
        { "Final Audition 2-2", "Final Audition Ep. 2-2" },
        { "Final Audition 2-X", "Final Audition Ep. 2-X" },
        { "Final Audition Ep 2-1", "Final Audition Ep. 2-1" },
        { "Final Audition Ep 2-2", "Final Audition Ep. 2-2" },
        { "Final Audition Ep 2-X", "Final Audition Ep. 2-X" },
        { "Final Audition Ep.1", "Final Audition Ep. 1" },
        { "Final Audition Episdoe 1", "Final Audition Ep. 1" },
        { "Final Audtion 2", "Final Audition 2" },
        { "Flew Far Faster", "FFF" },
        { "Four Seasons of Loneliness", "FOUR SEASONS OF LONELINESS verß feat. sariyajin" },
        {
            "Four Seasons Of Love", "FOUR SEASONS OF LONELINESS verß feat. sariyajin"
        },
        { "Get Up", "Get Up!" },
        { "Get Up and Go", "Get Up (and go)" },
        { "God Mode", "God Mode feat. skizzo" },
        { "God Mode 2.0 feat. Skizzo", "God Mode 2.0 " },
        { "Goodbye full Song", "Good Bye Full Song" },
        { "Hann (Alone)", "HANN" },
        { "Harmageddon", "Harmagedon" },
        { "Headess Chicken", "Headless Chicken" },
        { "Hi-Bi", "Hi Bi" },
        { "House Plan", "Houseplan" },
        { "Hyicanth", "Hyacinth" },
        { "Hypnosis (Synthwulf Mix)", "Hypnosis(SynthWulf Mix)" },
        { "Hypnosis (Synthwulf Remix)", "Hypnosis(SynthWulf Mix)" },
        { "Ideolized romance", "Idealized Romance" },
        { "Ignis Fatuus", "Ignis Fatuus(DM Ashura Mix)" },
        { "Ignis Fatuus (DM Ashura Mix)", "Ignis Fatuus(DM Ashura Mix)" },
        { "Im so sick", "I'm so sick" },
        { "Inteference", "Interference" },
        { "J Bong", "JBong" },
        { "Just Hold On", "Just Hold On (To All Fighters)" },
        { "K.O.A Alice in Wonderlan", "K.O.A : Alice In Wonderworld" },
        { "K.O.A Alice in Wonderland", "K.O.A : Alice In Wonderworld" },
        { "K.O.A. Alice in Wonderworld", "K.O.A : Alice In Wonderworld" },
        { "K.O.A.: Alice in Wonderworld", "K.O.A : Alice In Wonderworld" },
        { "Kariwisata", "Karyawisata" },
        { "Keep On", "Keep On!" },
        { "Kill Them", "Kill Them!" },
        { "La La", "Lala" },
        { "Log-In", "Log In" },
        { "LogIn", "Log In" },
        { "Love is a Danger Zone 2", "Love is a Danger Zone pt. 2" },
        { "Love is a Danger Zone 2 (Another)", "Love is a Danger Zone pt.2 another" },
        { "Love Is A Danger Zone 2 Try To B.P.M Remix", "Love is a danger zone (try to B.P.M.) Remix" },
        { "Love Is A Danger Zone 2 Try To BPM Remix", "Love is a danger zone (try to B.P.M.) Remix" },
        { "Lucid", "Lucid(PIU Edit)" },
        { "Lucid (PIU Edit)", "Lucid(PIU Edit)" },
        { "Macaroon Day", "Macaron Day" },
        { "Macraoon Day", "Macaron Day" },
        { "Miss S Story", "Miss S' story" },
        { "Mission Possible Blow Back", "Mission Possible -Blow Back-" },
        { "Moonlight Dance", "Moonlight" },
        { "Move That Body", "Move That Body!" },
        { "Mr Larpus", "Mr. Larpus" },
        { "Nakakapubagbuga", "Nakakapagpabagabag" },
        { "Nekkoya (Pick Me)", "Nekkoya" },
        { "Nice", "VERY NICE" },
        { "Nihilism", "Nihilism - Another Ver.-" },
        { "Nihilism - Another Ver. -", "Nihilism - Another Ver.-" },
        { "Nyarlothotep", "Nyarlathotep" },
        { "Oh! Rosa!", "Oh! Rosa" },
        { "Orbit Stabilzer", "Orbit Stabilizer" },
        { "Overblow 2", "Overblow2" },
        { "Papasito", "Papasito feat. KuTiNA" },
        { "Papasito (feat. KuTiNA)", "Papasito feat. KuTiNA" },
        { "Phalanx \"RS2018 edit\"", "Phalanx" },
        { "Phalanx \"RS2018\" edit", "Phalanx" },
        { "Phantom Intermezzo", "Phantom -Intermezzo-" },
        { "Pumptris 8Bit ver", "Pumptris 8Bit ver." },
        { "Pumptrips 8Bit ver.", "Pumptris 8Bit ver." },
        { "Queen of Red", "Queen of the Red" },
        { "Rave til the earth's end", "Rave 'til the Earth's End" },
        { "Rave until the Night's Over", "Rave 'til the Earth's End" },
        { "Rave'til the earth's end", "Rave 'til the Earth's End" },
        { "Red VS Blue", "Dual Racing <RED vs BLUE>" },
        { "Removable Disk 0", "Removable Disk0" },
        { "Renai Yuushu", "Renai Yuusha" },
        { "Scorpion", "Scorpion King" },
        { "Set me uo", "Set me up" },
        { "Sillhouette Effect", "Silhouette Effect" },
        { "Silver Beat", "Silver Beat feat. ChisaUezono" },
        { "SilverBeat feat. ChisaUezono", "Silver Beat feat. ChisaUezono" },
        { "Sora no Shirabe", "Sorano Shirabe" },
        { "Stardream (feat. Romelon)", "Stardream" },
        { "Street Showdown", "Street show down" },
        { "Sudden Romance", "Sudden Romance [PIU Edit]" },
        { "Sudden Romance (PIU Edit)", "Sudden Romance [PIU Edit]" },
        { "Sugar Conspiracy", "Sugar Conspiracy Theory" },
        { "Super Capaccio", "Super Capriccio" },
        { "Super Carpaccio", "Super Capriccio" },
        { "Super Stylin", "Super Stylin'" },
        { "Tek", "Tek -Club Copenhagen-" },
        { "The End of the World", "The End of the World ft. Skizzo" },
        { "The Festival of Ghost2 (Sneak)", "The Festival of Ghost 2 (Sneak)" },
        { "The Little Prince (Prod. Godic)", "The Little Prince" },
        { "The Quick Brown Fox", "The Quick Brown Fox Jumps Over The Lazy Dog" },
        { "Til the end of Time", "Till the end of time" },
        { "Time Attack", "Time Attack <Blue>" },
        { "Time Attack Blue", "Time Attack <Blue>" },
        { "TQBFJOTLD", "The Quick Brown Fox Jumps Over The Lazy Dog" },
        { "Transcaglia", "Transacaglia in G-minor" },
        {
            "Trashy Innocense", "Trashy Innocence"
        },
        { "Triton", "Tritium" },
        { "Turkey March Minimal Tunes", "Turkey March -Minimal Tunes-" },
        { "Twist of Fate", "Twist of Fate (feat. Ruriling)" },
        { "U Got Me Crazy", "You Got Me Crazy" },
        {
            "Up & Up", "Up & Up (Produced by AWAL)"
        },
        { "Utsushiyo no Kaze", "Utsushiyo No Kaze feat. Kana" },
        { "Utsushiyo No Kaze feat. Sana", "Utsushiyo No Kaze feat. Kana" },
        { "Video Display Out", "video out c" },
        { "Visual Dream II", "Visual Dream II (In Fiction)" },
        { "Visual Dream2 (In Fiction)", "Visual Dream II (In Fiction)" },
        { "Visual Effect", "Visual Dream II (In Fiction)" },
        { "Wil o the Wisp", "Will-O-The-Wisp" },
        { "Will O The Wisp", "Will-O-The-Wisp" },
        { "X-tream", "X Treme" },
        { "X-Tree", "XTree" },
        { "Yog Sothoth", "Yog-Sothoth" },
        { "Yoropiku Pikuyoro", "Yoropiku Pikuyoro!" },
        { "What Are You Doin Remix", "What Are You Doin? Remix" },
        { "Repeatorment Remix Remix", "Repeatorment Remix" },
        { "Meteo5cience (Gadget Mix) Remix", "Meteo5cience Remix" },
        { "MSGoon PT.6 Remix", "msgoon RMX pt.6 Remix" },
        { "msgoon pt 6 Remix", "msgoon RMX pt.6 Remix" },
        { "MSGoon RMX Pt.6 Remix", "msgoon RMX pt.6 Remix" },
        { "EXTRA BanYa Remix Remix", "EXTRA BanYa Remix" },
        { "The People didn't know \"Pumping up\" Remix", "The People didn't know Pumping up Remix" },
        { "Papasito (feat. KuTiNA) Full Song", "Papasito Full Song" },
        { "Papasito feat. KuTiNA Full Song", "Papasito Full Song" },
        { "NEKKOYA (PICK ME) Full Song", "NEKKOYA Full Song" },
        { "Move That Body Full Song", "Move That Body! Full Song" },
        { "I'm so sick Full Song", "I’m so sick Full Song" },
        { "FOUR SEASONS OF LONELINESS Full Song", "FOUR SEASONS OF LONELINESS verß feat. sariyajin Full Song" },
        { "Dignity Full Song", "DIGNITY FULL SONG MIX" },
        { "Yog Sothoth Short Cut", "Yog-Sothoth Short Cut" },
        { "Pumptris 8Bit Version Short Cut", "Pumptris 8Bit ver. Short Cut" },
        { "Love Is A Danger Zone 2 Short Cut", "Love is a Danger Zone pt. 2 Short Cut" },
        { "K.O.A.: Alice in Wonderworld Short Cut", "K.O.A : Alice in Wonderworld Short Cut" },
        { "K.O.A. Alice in Wonderworld Short Cut", "K.O.A : Alice in Wonderworld Short Cut" },
        { "Ignis Fatuus (DM Ashura Mix) Short Cut", "Ignis Fatuus(DM Ashura Mix) Short Cut" },
        { "Flew Far Faster Short Cut", "FFF Short Cut" },
        { "Final Audition 2 Short Cut", "Final Audition 2 Short Cut" },
        { "Final Audition 2-X Short Cut", "Final Audition EP. 2-X Short Cut" },
        { "Exceed 2 Opening Short Cut", "Exceed2 Opening Short Cut" },
        { "Exceed2 Opening Short Cut", "Exceed2 Opening Short Cut" },
        { "Extravaganza Shortcut", "Extravaganza Short Cut" },
        { "Ignis Fatuus Short Cut", "Ignis Fatuus(DM Ashura Mix) Short Cut" },
        { "K.O.A Short Cut", "K.O.A : Alice in Wonderworld Short Cut" },
        { "Prime Time", "Prime Time Remix" },
        { "Mopemope", "Mope Mope" },
        { "Re: End of a Dream", "Re : End of a Dream" }
    };

    private XXScoreFile(ScoreFileType type, IEnumerable<BestXXChartAttempt> scores,
        IEnumerable<SpreadsheetScoreErrorDto> errors)
    {
        FileType = type;
        Scores = scores.ToImmutableList();
        Errors = errors.ToImmutableList();
    }

    public ScoreFileType FileType { get; }

    public string TypeDescription => typeof(ScoreFileType).GetField(FileType.ToString())
        ?.GetCustomAttribute<DescriptionAttribute>()?.Description ?? "";

    public IImmutableList<BestXXChartAttempt> Scores { get; }
    public IImmutableList<SpreadsheetScoreErrorDto> Errors { get; }

    public static async Task<XXScoreFile> ReadAsync(IBrowserFile file, CancellationToken cancellationToken = default)
    {
        return file.ContentType.ToLower() switch
        {
            "text/csv" => await BuildFromCsv(file, cancellationToken),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" => await BuildFromExcel(file,
                cancellationToken),
            _ => throw new ScoreFileParseException($"Invalid file type {file.ContentType}")
        };
    }

    private static async Task<XXScoreFile> BuildFromExcel(IBrowserFile file,
        CancellationToken cancellationToken = default)
    {
        await using var readStream = file.OpenReadStream(MaxByteCount, cancellationToken);

        using var package = new ExcelPackage();
        await package.LoadAsync(readStream, cancellationToken);
        var result = new List<BestXXChartAttempt>();
        var errors = new List<SpreadsheetScoreErrorDto>();
        foreach (var workbook in package.Workbook.Worksheets)
        {
            if (workbook == null) continue;
            if (!DifficultyLevel.TryParseShortHand(workbook.Name, out var chartType, out var level)) continue;

            var (newAttempts, newErrors) = ExtractBestAttempts(chartType, level, workbook);
            result.AddRange(newAttempts);
            errors.AddRange(errors);
        }

        return new XXScoreFile(ScoreFileType.LetterGradeExcel, result, errors);
    }

    private static bool TryParseCellIntoBool(string text, out bool result)
    {
        result = false;
        if (int.TryParse(text, out var intResult))
        {
            result = intResult == 1;
            return true;
        }

        if (!bool.TryParse(text, out var boolResult)) return false;
        result = boolResult;
        return true;
    }

    private static (bool, XXLetterGrade?) GetScoreFromRow(ExcelWorksheet worksheet, int rowId)
    {
        if (TryParseCellIntoBool(worksheet.Cells[rowId, 2].Text, out var isBroken)
            && TryParseCellIntoBool(worksheet.Cells[rowId, 3].Text, out var isA)
            && TryParseCellIntoBool(worksheet.Cells[rowId, 4].Text, out var isS)
            && TryParseCellIntoBool(worksheet.Cells[rowId, 5].Text, out var isSS)
            && TryParseCellIntoBool(worksheet.Cells[rowId, 6].Text, out var isSSS))
        {
            XXLetterGrade? letterGrade = isSSS ? XXLetterGrade.SSS :
                isSS ? XXLetterGrade.SS :
                isS ? XXLetterGrade.S :
                isA ? XXLetterGrade.A :
                null;

            if (!isBroken && letterGrade == null) letterGrade = XXLetterGrade.A;

            return (isBroken, letterGrade);
        }

        var letterField = worksheet.Cells[rowId, 2].Text;
        if (string.IsNullOrWhiteSpace(letterField)) return (true, null);
        if (Enum.TryParse<XXLetterGrade>(letterField, out var grade)) return (false, grade);

        throw new Exception("Could not parse letter grade from row");
    }

    private static (IEnumerable<BestXXChartAttempt>, IEnumerable<SpreadsheetScoreErrorDto>) ExtractBestAttempts(
        ChartType category, DifficultyLevel level,
        ExcelWorksheet worksheet)
    {
        var currentType = category;
        var result = new List<BestXXChartAttempt>();
        var errors = new List<SpreadsheetScoreErrorDto>();
        var songNameSuffix = "";
        foreach (var rowId in Enumerable.Range(1, worksheet.Dimension.Rows))
        {
            var songNameField = worksheet.Cells[rowId, 1].Text ?? string.Empty;
            if (string.IsNullOrWhiteSpace(songNameField)) continue;

            if (songNameField.Trim().Equals("Arcade", StringComparison.OrdinalIgnoreCase)
                || songNameField.Trim().Equals("Full", StringComparison.OrdinalIgnoreCase)
                || songNameField.Trim().Equals("Full Song", StringComparison.OrdinalIgnoreCase)
                || songNameField.Trim().Equals("Shortcut", StringComparison.OrdinalIgnoreCase)
                || songNameField.Trim().Equals("Remix", StringComparison.OrdinalIgnoreCase))
            {
                currentType = category == ChartType.Single ? ChartType.Single : ChartType.Double;
                songNameSuffix = songNameField.Trim().ToLower() switch
                {
                    "full song" => " Full Song",
                    "full" => " Full Song",
                    "remix" => " Remix",
                    "shortcut" => " Short Cut",
                    _ => ""
                };
                continue;
            }

            if (songNameField.Trim().Equals("Performance", StringComparison.OrdinalIgnoreCase)
                || songNameField.Trim().Equals("Double Performance", StringComparison.OrdinalIgnoreCase)
                || songNameField.Trim().Equals("Single Performance", StringComparison.OrdinalIgnoreCase))
            {
                songNameSuffix = "";
                currentType = category == ChartType.Single ? ChartType.SinglePerformance : ChartType.DoublePerformance;
                continue;
            }

            bool isBroken;
            XXLetterGrade? letterGrade;
            try
            {
                (isBroken, letterGrade) = GetScoreFromRow(worksheet, rowId);
            }
            catch (Exception)
            {
                errors.Add(new SpreadsheetScoreErrorDto
                {
                    Song = songNameField,
                    Difficulty = level.ToString(),
                    LetterGrade = "Unknown",
                    IsBroken = "Unknown",
                    Error = "Could not parse score"
                });
                continue;
            }

            if (!Name.TryParse(songNameField, out var name))
            {
                errors.Add(new SpreadsheetScoreErrorDto
                {
                    Difficulty = level.ToString(),
                    LetterGrade = letterGrade?.ToString() ?? "",
                    IsBroken = isBroken.ToString(),
                    Song = songNameField,
                    Error = "Could not parse song name"
                });
                continue;
            }

            name += songNameSuffix;
            if (NameMappings.ContainsKey(name)) name = NameMappings[name];
            XXChartAttempt? attempt = null;
            if (letterGrade != null)
                attempt = new XXChartAttempt(letterGrade.Value, isBroken, null, DateTimeOffset.Now);


            result.Add(new BestXXChartAttempt(
                new Chart(Guid.Empty,
                    new Song(name, SongType.Arcade, new Uri("/", UriKind.Relative), TimeSpan.Zero, "Unknown", null),
                    currentType,
                    level, null), attempt));
        }

        return (result, errors);
    }

    private static async Task<XXScoreFile> BuildFromCsv(IBrowserFile file,
        CancellationToken cancellationToken = default)
    {
        await using var readStream = file.OpenReadStream(MaxByteCount, cancellationToken);
        using var reader = new StreamReader(readStream);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
        var scores = new List<BestXXChartAttempt>();
        var failures = new List<SpreadsheetScoreErrorDto>();
        await csv.ReadAsync();
        csv.ReadHeader();

        if (!csv.TryGetField<string>(nameof(XXSpreadsheetScoreDto.Song), out var _))
            throw new ScoreFileParseException("Spreadsheet is missing Song column");
        if (!csv.TryGetField<string>(nameof(XXSpreadsheetScoreDto.Difficulty), out _))
            throw new ScoreFileParseException("Spreadsheet is missing Difficulty column");
        if (!csv.TryGetField<string>(nameof(XXSpreadsheetScoreDto.LetterGrade), out _))
            throw new ScoreFileParseException("Spreadsheet is missing XXLetterGrade column");


        await foreach (var record in csv.GetRecordsAsync<XXSpreadsheetScoreDto>(cancellationToken))
        {
            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException("Cancellation was requested");
            try
            {
                var isBroken = false;

                if (!string.IsNullOrWhiteSpace(record.IsBroken))
                    if (!TryParseCellIntoBool(record.IsBroken, out isBroken))
                    {
                        failures.Add(record.ToError("Could not parse IsBroken Column"));
                        continue;
                    }

                if (!XXScore.TryParse(record.Score, out XXScore? score))
                {
                    failures.Add(record.ToError("Could not parse Score Column"));
                    continue;
                }


                var name = (Name)record.Song;
                if (NameMappings.ContainsKey(name)) name = NameMappings[name];
                var (chartType, level) = DifficultyLevel.ParseShortHand(record.Difficulty);
                var attempt = new BestXXChartAttempt(
                    new Chart(Guid.Empty,
                        new Song(name, SongType.Arcade, new Uri("/", UriKind.Relative), TimeSpan.Zero, "Unknown", null),
                        chartType,
                        level, null),
                    string.IsNullOrWhiteSpace(record.LetterGrade)
                        ? null
                        : new XXChartAttempt(Enum.Parse<XXLetterGrade>(record.LetterGrade, true), isBroken, score,
                            DateTimeOffset.Now));

                scores.Add(attempt);
            }
            catch (Exception)
            {
                failures.Add(record.ToError("Could not parse row"));
            }
        }

        return new XXScoreFile(ScoreFileType.LetterGradeCsv, scores, failures);
    }
}

public sealed class ScoreFileParseException : Exception
{
    public ScoreFileParseException(string error) : base(error)
    {
    }
}

public enum ScoreFileType
{
    [Description("Unknown")] Unknown,
    [Description("Letter Grade CSV")] LetterGradeCsv,
    [Description("Letter Grade Excel")] LetterGradeExcel
}