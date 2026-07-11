using PumpoutExtractor;

if (args.Length < 3)
{
    Console.WriteLine("Usage: PumpoutExtractor <pumpout.db> <prodExportDir> <outDir> [aliases.json]");
    Console.WriteLine();
    Console.WriteLine("  pumpout.db     — a dump from github.com/AnyhowStep/pump-out-sqlite3-dump");
    Console.WriteLine("  prodExportDir  — folder with songs.json/charts.json/chartmixes.json from dev/export/*");
    Console.WriteLine("  outDir         — receives the S1/S2/S3 scripts + reports/ (point at Downloads)");
    return 1;
}

var dumpPath = args[0];
var prodDir = args[1];
var outDir = args[2];
var aliasPath = args.Length > 3 ? args[3] : Path.Combine(AppContext.BaseDirectory, "aliases.json");
Directory.CreateDirectory(outDir);
var reportDir = Path.Combine(outDir, "reports");
Directory.CreateDirectory(reportDir);

Console.WriteLine("Loading pumpout dump...");
var dump = new PumpoutDump(dumpPath);
MatcherContext.Initialize(dump);
Console.WriteLine($"  {dump.Songs.Count} songs, {dump.Charts.Count} charts");

Console.WriteLine("Loading prod export...");
var prod = new ProdExport(prodDir);
Console.WriteLine($"  {prod.Songs.Count} songs, {prod.Charts.Count} charts, {prod.ChartMixes.Count} chartmix rows");

Console.WriteLine("Matching...");
var aliases = Matcher.LoadAliases(aliasPath);
Console.WriteLine($"  {aliases.Count} aliases loaded");
var matcher = new Matcher(prod, dump, aliases);
Console.WriteLine($"  songs matched: {matcher.SongMatches.Count}; charts matched: {matcher.ChartMatches.Count}");
Console.WriteLine($"  prod residual XX rows: {matcher.ProdResiduals.Count}; suspects quarantined: {matcher.Suspects.Count}");

var debuts = dump.Debuts();
var skipped = new List<string>();
var s3Report = new List<string>();
var artNeeded = new List<string>();

File.WriteAllText(Path.Combine(outDir, "s1-pumpout-catalog-corrections.sql"), SqlEmit.Corrections(matcher, skipped));
File.WriteAllText(Path.Combine(outDir, "s2-originalmix-backfill.sql"), SqlEmit.OriginalMixBackfill(matcher, debuts));
File.WriteAllText(Path.Combine(outDir, "s3-membership-backfill.sql"),
    SqlEmit.MembershipBackfill(matcher, dump, debuts, s3Report, artNeeded));

File.WriteAllLines(Path.Combine(reportDir, "notes.txt"), matcher.Notes);
File.WriteAllLines(Path.Combine(reportDir, "prod-residuals.txt"),
    matcher.ProdResiduals.Select(r => $"{r.Song}\t{r.Chart}"));
File.WriteAllLines(Path.Combine(reportDir, "suspects.txt"),
    matcher.Suspects.Select(s => $"PROD: {s.ProdSong} {s.ProdChart}  <->  PUMPOUT: {s.PumpoutSong} {s.PumpoutChart}  [{s.Reason}]"));
File.WriteAllLines(Path.Combine(reportDir, "s3-report.txt"), s3Report);
File.WriteAllLines(Path.Combine(reportDir, "art-needed.txt"), artNeeded);
File.WriteAllLines(Path.Combine(reportDir, "skipped.txt"), skipped);

Console.WriteLine();
Console.WriteLine($"Scripts written to {outDir}:");
Console.WriteLine("  s1-pumpout-catalog-corrections.sql (schema-independent)");
Console.WriteLine("  s2-originalmix-backfill.sql        (needs the Mix-seeding migration live)");
Console.WriteLine("  s3-membership-backfill.sql         (needs LegacySlot/PlayerCount/BestAttempt.MixId live)");
Console.WriteLine($"Reports in {reportDir} — REVIEW suspects.txt and s3-report.txt before running S3.");
if (s3Report.Count > 0) Console.WriteLine($"  {s3Report[0]}");
return 0;
