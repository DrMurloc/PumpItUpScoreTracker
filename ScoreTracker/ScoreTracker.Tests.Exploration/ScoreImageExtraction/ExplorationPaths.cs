namespace ScoreTracker.Tests.Exploration.ScoreImageExtraction;

/// <summary>
///     Local inputs for the score-image exploration. Result-screen photos and the Tesseract
///     language model are machine-local (never committed); tests skip gracefully when absent.
///     Override via environment variables when the defaults don't match the machine.
/// </summary>
public static class ExplorationPaths
{
    public static string ImagesDirectory =>
        Environment.GetEnvironmentVariable("PIU_SCORE_IMAGES")
        ?? @"C:\Users\jonec\piu-ocr-poc\phoenix";

    public static string TessdataDirectory =>
        Environment.GetEnvironmentVariable("PIU_TESSDATA")
        ?? @"C:\Users\jonec\piu-ocr-poc\tessdata";

    public static string FixturesDirectory =>
        Path.Combine(AppContext.BaseDirectory, "ScoreImageExtraction", "Fixtures");

    /// <summary>Repo-relative snapshot folder (committed) — one JSON per extractor iteration.</summary>
    public static string SnapshotsDirectory
    {
        get
        {
            // Walk up from bin/ to the project folder so snapshots land in source control.
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && dir.Name != "ScoreTracker.Tests.Exploration")
                dir = dir.Parent;
            return dir is null
                ? Path.Combine(AppContext.BaseDirectory, "Snapshots")
                : Path.Combine(dir.FullName, "ScoreImageExtraction", "Snapshots");
        }
    }

    public static bool InputsAvailable(out string reason)
    {
        if (!Directory.Exists(ImagesDirectory))
        {
            reason = $"images folder not found: {ImagesDirectory}";
            return false;
        }

        if (!File.Exists(Path.Combine(TessdataDirectory, "eng.traineddata")))
        {
            reason = $"tessdata not found: {TessdataDirectory}\\eng.traineddata";
            return false;
        }

        reason = "";
        return true;
    }
}
