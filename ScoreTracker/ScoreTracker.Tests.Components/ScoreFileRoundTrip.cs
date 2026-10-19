using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bunit;
using MediatR;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
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
///     A CSV the site hands out, offered back to the upload: the bytes a download gave the browser, read by
///     the real <see cref="PhoenixScoreFileExtractor" /> against a catalog the test supplies.
/// </summary>
internal static class ScoreFileRoundTrip
{
    public static Chart CatalogChart(MixEnum mix, string song, ChartType type, int level)
    {
        return new Chart(Guid.NewGuid(), mix,
            new Song(song, SongType.Arcade, new Uri("https://piu.test/art.png"),
                TimeSpan.FromMinutes(2), "Artist", Bpm.From(150, 150)),
            type, level, mix, null, 1200);
    }

    /// <summary>The file a <c>downloadFileFromStream</c> call carried, byte-order mark included.</summary>
    public static byte[] DownloadedBytes(JSRuntimeInvocation download)
    {
        return Assert.IsType<MemoryStream>(Assert.IsType<DotNetStreamReference>(download.Arguments[1]).Stream)
            .ToArray();
    }

    public static Task<(IEnumerable<RecordedPhoenixScore> Scores, IEnumerable<SpreadsheetScoreErrorDto> Errors)>
        ReadBack(byte[] file, MixEnum mix, IReadOnlyCollection<Chart> catalog)
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<GetChartQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GetChartQuery q, CancellationToken _) =>
                catalog.SingleOrDefault(c => c.Song.Name == q.SongName && c.Type == q.Type && c.Level == q.Level));
        return new PhoenixScoreFileExtractor(mediator.Object).GetScores(new DownloadedFile(file), mix,
            CancellationToken.None);
    }

    private sealed class DownloadedFile : IBrowserFile
    {
        private readonly byte[] _bytes;

        public DownloadedFile(byte[] bytes)
        {
            _bytes = bytes;
        }

        public string Name => "scores.csv";
        public DateTimeOffset LastModified => DateTimeOffset.UnixEpoch;
        public long Size => _bytes.Length;
        public string ContentType => "text/csv";

        public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default)
        {
            return new MemoryStream(_bytes);
        }
    }
}
