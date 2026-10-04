using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Bunit;
using CsvHelper;
using Microsoft.JSInterop;
using ScoreTracker.Web.Dtos;
using ScoreTracker.Web.Services;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The one path every record download on the site writes through: Account's scores.csv, the
///     upload pages' failedUploads.csv and the XX example file. Excel opens a CSV in the system
///     codepage unless it starts with the UTF-8 mark, and players edit these files there and
///     upload them again.
/// </summary>
public sealed class CsvTextTests : TestContext
{
    private const string Title = "Simon Says, EURODANCE!! (feat. Sara☆M)";

    private static readonly byte[] ByteOrderMark = { 0xEF, 0xBB, 0xBF };

    private static SpreadsheetScoreErrorDto[] Rows() => new[]
    {
        new SpreadsheetScoreErrorDto { Song = Title, Difficulty = "D24", Error = "Chart not found" }
    };

    [Fact]
    public async Task ARecordFileStartsWithTheByteOrderMarkAndThenItsHeader()
    {
        var bytes = await CsvText.ToBytesAsync(Rows());

        Assert.Equal(ByteOrderMark, bytes.Take(3).ToArray());
        // One mark, not two: the header follows it directly.
        Assert.StartsWith("Difficulty,Song,", Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3));
    }

    [Fact]
    public async Task ANonAsciiTitleWithACommaReadsBackIntact()
    {
        var bytes = await CsvText.ToBytesAsync(Rows());

        using var csv = new CsvReader(new StreamReader(new MemoryStream(bytes), Encoding.UTF8, true),
            CultureInfo.InvariantCulture);
        var row = Assert.Single(csv.GetRecords<SpreadsheetScoreErrorDto>().ToArray());
        Assert.Equal(Title, row.Song);
        Assert.Equal("D24", row.Difficulty);
        Assert.Equal("Chart not found", row.Error);
    }

    [Fact]
    public async Task ADownloadHandsTheBrowserTheNamedFileWithItsMark()
    {
        var module = JSInterop.SetupModule("./js/helpers.js");
        module.SetupVoid("downloadFileFromStream", _ => true).SetVoidResult();

        await CsvText.DownloadAsync(JSInterop.JSRuntime, "failedUploads.csv", Rows());

        var invocation = module.VerifyInvoke("downloadFileFromStream");
        Assert.Equal("failedUploads.csv", invocation.Arguments[0]);
        var stream = Assert.IsType<MemoryStream>(Assert.IsType<DotNetStreamReference>(invocation.Arguments[1]).Stream);
        Assert.Equal(await CsvText.ToBytesAsync(Rows()), stream.ToArray());
    }
}
