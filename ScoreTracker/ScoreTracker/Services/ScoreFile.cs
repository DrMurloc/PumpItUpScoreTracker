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

public sealed class ScoreFile
{
    public const int MaxByteCount = 10000000;

    private ScoreFile(ScoreFileType type, IEnumerable<BestChartAttempt> scores,
        IEnumerable<SpreadsheetScoreErrorDto> errors)
    {
        FileType = type;
        Scores = scores.ToImmutableList();
        Errors = errors.ToImmutableList();
    }

    public ScoreFileType FileType { get; }

    public string TypeDescription => typeof(ScoreFileType).GetField(FileType.ToString())
        ?.GetCustomAttribute<DescriptionAttribute>()?.Description ?? "";

    public IImmutableList<BestChartAttempt> Scores { get; }
    public IImmutableList<SpreadsheetScoreErrorDto> Errors { get; }

    public static async Task<ScoreFile> ReadAsync(IBrowserFile file, CancellationToken cancellationToken = default)
    {
        return file.ContentType.ToLower() switch
        {
            "text/csv" => await BuildFromCsv(file, cancellationToken),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" => await BuildFromExcel(file,
                cancellationToken),
            _ => throw new ScoreFileParseException($"Invalid file type {file.ContentType}")
        };
    }

    private static async Task<ScoreFile> BuildFromExcel(IBrowserFile file,
        CancellationToken cancellationToken = default)
    {
        await using var readStream = file.OpenReadStream(MaxByteCount, cancellationToken);

        using var package = new ExcelPackage();
        await package.LoadAsync(readStream, cancellationToken);
        var result = new List<BestChartAttempt>();
        var errors = new List<SpreadsheetScoreErrorDto>();
        foreach (var workbook in package.Workbook.Worksheets)
        {
            if (workbook == null) continue;
            if (!DifficultyLevel.TryParseShortHand(workbook.Name, out var chartType, out var level)) continue;

            var (newAttempts, newErrors) = ExtractBestAttempts(chartType, level, workbook);
            result.AddRange(newAttempts);
            errors.AddRange(errors);
        }

        return new ScoreFile(ScoreFileType.LetterGradeExcel, result, errors);
    }

    private static (IEnumerable<BestChartAttempt>, IEnumerable<SpreadsheetScoreErrorDto>) ExtractBestAttempts(
        ChartType category, DifficultyLevel level,
        ExcelWorksheet worksheet)
    {
        var currentType = category;
        var result = new List<BestChartAttempt>();
        var errors = new List<SpreadsheetScoreErrorDto>();
        var songNameSuffix = "";
        foreach (var rowId in Enumerable.Range(1, worksheet.Dimension.Rows))
        {
            var songNameField = worksheet.Cells[rowId, 1].Text ?? string.Empty;
            if (string.IsNullOrWhiteSpace(songNameField)) continue;
            var letterField = worksheet.Cells[rowId, 2].Text ?? string.Empty;
            if (songNameField.Equals("Arcade", StringComparison.OrdinalIgnoreCase)
                || songNameField.Equals("Full", StringComparison.OrdinalIgnoreCase)
                || songNameField.Equals("Shortcut", StringComparison.OrdinalIgnoreCase)
                || songNameField.Equals("Remix", StringComparison.OrdinalIgnoreCase))
            {
                currentType = category == ChartType.Single ? ChartType.Single : ChartType.Double;
                songNameSuffix = songNameField.ToLower() switch
                {
                    "full" => " Full Song",
                    "remix" => " Remix",
                    "shortcut" => " Short Cut",
                    _ => ""
                };
                continue;
            }

            if (songNameField.Equals("Performance"))
            {
                songNameSuffix = "";
                currentType = category == ChartType.Single ? ChartType.SinglePerformance : ChartType.DoublePerformance;
                continue;
            }

            if (!Name.TryParse(songNameField, out var name))
            {
                errors.Add(new SpreadsheetScoreErrorDto
                {
                    Difficulty = level.ToString(),
                    LetterGrade = letterField,
                    Song = songNameField,
                    Error = "Could not parse song name"
                });
                continue;
            }

            name += songNameSuffix;
            ChartAttempt? attempt = null;
            if (Enum.TryParse<LetterGrade>(letterField, out var letterGrade))
            {
                attempt = new ChartAttempt(letterGrade, false);
            }
            else if (!string.IsNullOrWhiteSpace(letterField))
            {
                errors.Add(new SpreadsheetScoreErrorDto
                {
                    Difficulty = level.ToString(),
                    LetterGrade = letterField,
                    Song = songNameField,
                    Error = "Could not parse letter grade"
                });
                continue;
            }


            result.Add(new BestChartAttempt(
                new Chart(new Song(name, new Uri("/", UriKind.Relative)), currentType, level), attempt));
        }

        return (result, errors);
    }

    private static async Task<ScoreFile> BuildFromCsv(IBrowserFile file, CancellationToken cancellationToken = default)
    {
        await using var readStream = file.OpenReadStream(MaxByteCount, cancellationToken);
        using var reader = new StreamReader(readStream);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
        var scores = new List<BestChartAttempt>();
        var failures = new List<SpreadsheetScoreErrorDto>();
        await csv.ReadAsync();
        csv.ReadHeader();

        if (!csv.TryGetField<string>(nameof(SpreadsheetScoreDto.Song), out var _))
            throw new ScoreFileParseException("Spreadsheet is missing Song column");
        if (!csv.TryGetField<string>(nameof(SpreadsheetScoreDto.Difficulty), out _))
            throw new ScoreFileParseException("Spreadsheet is missing Difficulty column");
        if (!csv.TryGetField<string>(nameof(SpreadsheetScoreDto.LetterGrade), out _))
            throw new ScoreFileParseException("Spreadsheet is missing LetterGrade column");

        await foreach (var record in csv.GetRecordsAsync<SpreadsheetScoreDto>(cancellationToken))
        {
            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException("Cancellation was requested");
            try
            {
                scores.Add(record.ToBestAttempt());
            }
            catch (Exception ex)
            {
                failures.Add(record.ToError("Could not parse row"));
            }
        }

        return new ScoreFile(ScoreFileType.LetterGradeCsv, scores, failures);
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