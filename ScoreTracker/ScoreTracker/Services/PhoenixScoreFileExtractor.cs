using CsvHelper;
using MediatR;
using Microsoft.AspNetCore.Components.Forms;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.ValueTypes;
using ScoreTracker.Web.Dtos;
using ScoreTracker.Web.Services.Contracts;
using System.Globalization;

namespace ScoreTracker.Web.Services;

public sealed class PhoenixScoreFileExtractor : IPhoenixScoreFileExtractor
{
    private readonly IMediator _mediator;

    public PhoenixScoreFileExtractor(IMediator mediator)
    {
        _mediator = mediator;
    }

    public async Task<(IEnumerable<RecordedPhoenixScore> Scores, IEnumerable<SpreadsheetScoreErrorDto> Errors)>
        GetScores(IBrowserFile file, CancellationToken cancellationToken = default)
    {
        await using var readStream = file.OpenReadStream(XXScoreFile.MaxByteCount, cancellationToken);
        using var reader = new StreamReader(readStream);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
        var scores = new List<RecordedPhoenixScore>();
        var failures = new List<SpreadsheetScoreErrorDto>();
        await csv.ReadAsync();
        csv.ReadHeader();

        if (!csv.TryGetField<string>(nameof(PhoenixSpreadsheetScoreDto.Song), out var _))
            throw new ScoreFileParseException("Spreadsheet is missing Song column");
        if (!csv.TryGetField<string>(nameof(PhoenixSpreadsheetScoreDto.Difficulty), out _))
            throw new ScoreFileParseException("Spreadsheet is missing Difficulty column");
        if (!csv.TryGetField<string>(nameof(PhoenixSpreadsheetScoreDto.Score), out _))
            throw new ScoreFileParseException("Spreadsheet is missing Score column");
        if (!csv.TryGetField<string>(nameof(PhoenixSpreadsheetScoreDto.Plate), out _))
            throw new ScoreFileParseException("Spreadsheet is missing Plate column");


        await foreach (var record in csv.GetRecordsAsync<PhoenixSpreadsheetScoreDto>(cancellationToken))
        {
            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException("Cancellation was requested");
            try
            {
                if (!PhoenixScore.TryParse(record.Score, out PhoenixScore? score))
                {
                    failures.Add(record.ToError("Could not parse Score Column"));
                    continue;
                }

                var plate = record.Plate.Trim().ToLower() switch
                {
                    "rg" => PhoenixPlate.RoughGame,
                    "tg" => PhoenixPlate.TalentedGame,
                    "pg" => PhoenixPlate.PerfectGame,
                    "ug" => PhoenixPlate.UltimateGame,
                    "fg" => PhoenixPlate.FairGame,
                    "sg" => PhoenixPlate.SuperbGame,
                    "eg" => PhoenixPlate.ExtremeGame,
                    "mg" => PhoenixPlate.MarvelousGame,
                    _ => throw new ScoreFileParseException("Plate is invalid")
                };

                var name = (Name)record.Song;
                var (chartType, level) = DifficultyLevel.ParseShortHand(record.Difficulty);
                //These keep coming over with corrupted strings?
                if (name.ToString().StartsWith("Cross Ray (feat", StringComparison.OrdinalIgnoreCase))
                    name = "Cross Ray";
                if (name.ToString().Equals("Witch Doctor Num1", StringComparison.OrdinalIgnoreCase))
                    name = "Witch Doctor #1";
                if (name.ToString().EndsWith("End of a Dream", StringComparison.OrdinalIgnoreCase))
                    name = "Re:End of a Dream";
                var chart = await _mediator.Send(new GetChartQuery(MixEnum.Phoenix, name, level, chartType),
                    cancellationToken);
                if (chart == null)
                    throw new ScoreFileParseException($"This chart was not found: {name} {chartType} {level}");

                var attempt = new RecordedPhoenixScore(chart.Id, score, plate, false, DateTimeOffset.Now);

                scores.Add(attempt);
            }
            catch (Exception ex)
            {
                failures.Add(record.ToError("Could not parse row"));
            }
        }

        return (scores, failures);
    }
}