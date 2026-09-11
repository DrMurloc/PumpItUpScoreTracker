using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Components.Forms;
using Moq;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Dtos;
using ScoreTracker.Web.Services;
using Xunit;
using ChartType = ScoreTracker.SharedKernel.Enums.ChartType;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The manual import's CSV reader (docs/design/import-scores-refresh.md): what a row must
///     carry to become a score, and what a broken row may leave out.
/// </summary>
public sealed class PhoenixScoreFileExtractorTests
{
    private const string Header = "Song,Difficulty,Score,LetterGrade,Plate,IsBroken\r\n";

    private readonly Mock<IMediator> _mediator = new();

    public PhoenixScoreFileExtractorTests()
    {
        var chart = new Chart(Guid.NewGuid(), MixEnum.Phoenix2,
            new Song("Arcana Force", SongType.Arcade, new Uri("https://piu.test/art.png"),
                TimeSpan.FromMinutes(2), "Artist", Bpm.From(170, 170)),
            ChartType.Double, 20, MixEnum.Phoenix2, null, 1200);
        _mediator.Setup(m => m.Send(It.IsAny<GetChartQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(chart);
    }

    [Fact]
    public async Task ABrokenRowNeedsNoPlateAndKeepsNone()
    {
        // A failed stage is awarded no plate — the Phoenix 2 best list prints an empty slot.
        var (scores, errors) = await Extract(Header + "\"Arcana Force\",D20,590032,x_d,,true\r\n");

        var score = Assert.Single(scores);
        Assert.Empty(errors);
        Assert.True(score.IsBroken);
        Assert.Null(score.Plate);
        Assert.Equal(590032, (int)score.Score!.Value);
    }

    [Fact]
    public async Task APassingRowStillNeedsAPlate()
    {
        var (scores, errors) = await Extract(Header + "\"Arcana Force\",D20,990032,sss,,false\r\n");

        Assert.Empty(scores);
        Assert.Single(errors);
    }

    [Fact]
    public async Task AFileWithoutTheIsBrokenColumnReadsEveryRowAsAPass()
    {
        // Every CSV the script wrote before the column existed, and every hand-kept spreadsheet.
        var (scores, errors) = await Extract(
            "Song,Difficulty,Score,LetterGrade,Plate\r\n\"Arcana Force\",D20,990032,sss,fg\r\n");

        var score = Assert.Single(scores);
        Assert.Empty(errors);
        Assert.False(score.IsBroken);
        Assert.Equal(PhoenixPlate.FairGame, score.Plate);
    }

    [Fact]
    public async Task ChartsResolveInTheMixTheFileIsUploadedTo()
    {
        await Extract(Header + "\"Arcana Force\",D20,990032,sss,fg,false\r\n", MixEnum.Phoenix2);

        _mediator.Verify(m => m.Send(
            It.Is<GetChartQuery>(q => q.Mix == MixEnum.Phoenix2 && q.Type == ChartType.Double && (int)q.Level == 20),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private Task<(IEnumerable<RecordedPhoenixScore> Scores, IEnumerable<SpreadsheetScoreErrorDto> Errors)> Extract(
        string csv, MixEnum mix = MixEnum.Phoenix2)
    {
        return new PhoenixScoreFileExtractor(_mediator.Object).GetScores(new CsvFile(csv), mix,
            CancellationToken.None);
    }

    private sealed class CsvFile : IBrowserFile
    {
        private readonly byte[] _bytes;

        public CsvFile(string content)
        {
            _bytes = Encoding.UTF8.GetBytes(content);
        }

        public string Name => "piu-scores.csv";
        public DateTimeOffset LastModified => DateTimeOffset.UnixEpoch;
        public long Size => _bytes.Length;
        public string ContentType => "text/csv";

        public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default)
        {
            return new MemoryStream(_bytes);
        }
    }
}
