using System.Collections.Immutable;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using CsvHelper;
using ScoreTracker.Domain.Models;
using ScoreTracker.Web.Dtos;

namespace ScoreTracker.Web.Services;

public sealed class ScoreFile
{
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

    public static async Task<ScoreFile> ReadAsync(Stream input, CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(input);
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
    [Description("Letter Grade CSV")] LetterGradeCsv
}