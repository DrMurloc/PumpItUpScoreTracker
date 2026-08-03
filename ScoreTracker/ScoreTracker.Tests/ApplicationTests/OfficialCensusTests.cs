using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.OfficialMirror.Domain;
using ScoreTracker.OfficialMirror.Infrastructure;
using ScoreTracker.OfficialMirror.Infrastructure.Apis.Contracts;
using ScoreTracker.OfficialMirror.Infrastructure.Apis.Dtos;
using ScoreTracker.OfficialMirror.Wiring;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Tests.TestHelpers;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

/// <summary>
///     Assembling the OFFICIAL side of the completeness census: which buckets get read, how the
///     mixes' different level filters are handled, and that nothing here consults our records —
///     the comparison is a pure function over two censuses and this half only fetches one.
/// </summary>
public sealed class OfficialCensusTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 2, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public async Task EveryBucketTheSiteOffersIsReadAndTheAggregatesAreNot()
    {
        var api = Api(MixEnum.Phoenix2,
            Buckets("", "17", "18", "27over", "10over", "coop"),
            passes: new Dictionary<string, int> { ["17"] = 5, ["18"] = 3, ["27over"] = 0, ["coop"] = 2 });

        var census = await Client(api).GetOfficialCensus(MixEnum.Phoenix2, UserId, "sid", CancellationToken.None);

        // "" is the whole page and "10over" re-sums the numeric buckets — reading either as a
        // level would double every chart in the account.
        Assert.Equal(new[] { "17", "18", "27over", "coop" }, census.Buckets.Keys.OrderBy(k => k).ToArray());
        Assert.Equal(10, census.TotalPasses);
        api.Verify(a => a.GetPlayData(MixEnum.Phoenix2, It.IsAny<HttpClient>(), "10over",
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PhoenixRecoversSubTenFromTheBestScoreTotalItsPlayDataPageRefusesToBreakDown()
    {
        // Its level filter starts at 10 ("Levels 1~9 are not included and are not shown"), so the
        // only statement the site makes about those clears is the best list's Total.
        var api = Api(MixEnum.Phoenix,
            Buckets("", "10", "18", "27over", "10over", "coop"),
            passes: new Dictionary<string, int> { ["10"] = 127, ["18"] = 366, ["27over"] = 0, ["coop"] = 50 },
            bestScoreTotal: 618);

        var census = await Client(api).GetOfficialCensus(MixEnum.Phoenix, UserId, "sid", CancellationToken.None);

        // 618 total − (127 + 366 + 0 + 50) = 75 clears below level 10.
        Assert.Equal(75, census.For("sub10").Passes);
        Assert.Equal(618, census.TotalPasses);
    }

    [Fact]
    public async Task Phoenix2SkipsTheBestScoreRequestBecauseItBucketsEveryLevel()
    {
        // Its filter reaches level 1, so there is no residual to recover — and its best list
        // counts stage breaks, which would make the same subtraction wrong anyway.
        var api = Api(MixEnum.Phoenix2,
            Buckets("", "1", "9", "17", "27over", "10over", "coop"),
            passes: new Dictionary<string, int> { ["1"] = 1, ["9"] = 1, ["17"] = 5 });

        var census = await Client(api).GetOfficialCensus(MixEnum.Phoenix2, UserId, "sid", CancellationToken.None);

        Assert.False(census.Buckets.ContainsKey("sub10"));
        api.Verify(a => a.GetBestScores(It.IsAny<MixEnum>(), It.IsAny<HttpClient>(), It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ABestScoreTotalThatDoesNotExceedTheBucketsAddsNoResidual()
    {
        // Phoenix's own Total. equalled its buckets exactly on an account with no sub-10 clears;
        // a zero or negative residual is not a bucket.
        var api = Api(MixEnum.Phoenix,
            Buckets("", "10", "27over", "10over", "coop"),
            passes: new Dictionary<string, int> { ["10"] = 100 },
            bestScoreTotal: 100);

        var census = await Client(api).GetOfficialCensus(MixEnum.Phoenix, UserId, "sid", CancellationToken.None);

        Assert.False(census.Buckets.ContainsKey("sub10"));
    }

    [Fact]
    public async Task ThePumbilityHeadlineComesFromTheLivePoolPageNotTheRankingBoard()
    {
        var api = Api(MixEnum.Phoenix, Buckets("", "18"),
            passes: new Dictionary<string, int> { ["18"] = 1 }, bestScoreTotal: 1, pumbility: 64466);

        var census = await Client(api).GetOfficialCensus(MixEnum.Phoenix, UserId, "sid", CancellationToken.None);

        Assert.Equal(64466, census.Pumbility);
        // The ranking board is a daily 01:00 KST batch and would report a player who played today
        // as mismatched against their own scores.
        api.Verify(a => a.GetPumbilityRankings(It.IsAny<MixEnum>(), It.IsAny<ChartType?>(), It.IsAny<int>(),
            It.IsAny<HttpClient>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProgressIsReportedSoALongCensusDoesNotLookStalled()
    {
        var mediator = new Mock<IMediator>();
        var api = Api(MixEnum.Phoenix2, Buckets("", "17", "18"),
            passes: new Dictionary<string, int> { ["17"] = 5, ["18"] = 3 });

        await Client(api, mediator).GetOfficialCensus(MixEnum.Phoenix2, UserId, "sid", CancellationToken.None);

        mediator.Verify(m => m.Publish(
            It.Is<ImportStatusUpdatedEvent>(e => e.UserId == UserId && e.Mix == MixEnum.Phoenix2),
            It.IsAny<CancellationToken>()), Times.AtLeast(2));
    }

    [Fact]
    public async Task TheCensusNeverReadsOurStoredScores()
    {
        var scores = new Mock<IScoreReader>(MockBehavior.Strict);
        var api = Api(MixEnum.Phoenix2, Buckets("", "17"),
            passes: new Dictionary<string, int> { ["17"] = 5 });

        await Client(api, scores: scores).GetOfficialCensus(MixEnum.Phoenix2, UserId, "sid", CancellationToken.None);

        // Strict mock: any call would have thrown. Keeping the official read pure is what lets
        // the comparison itself be a unit-testable function.
        scores.VerifyNoOtherCalls();
    }

    // ---- builders ----

    private static string[] Buckets(params string[] buckets)
    {
        return buckets;
    }

    private static Mock<IPiuGameApi> Api(MixEnum mix, string[] buckets, IReadOnlyDictionary<string, int> passes,
        int? bestScoreTotal = null, double pumbility = 0)
    {
        var api = new Mock<IPiuGameApi>();
        api.Setup(a => a.ClientForSid(It.IsAny<MixEnum>(), It.IsAny<string>())).Returns(new HttpClient());
        api.Setup(a => a.GetPlayData(mix, It.IsAny<HttpClient>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MixEnum _, HttpClient _, string bucket, CancellationToken _) =>
                new PiuGameGetPlayDataResult
                {
                    Bucket = bucket,
                    Passes = passes.TryGetValue(bucket, out var count) ? count : 0,
                    Buckets = buckets
                });
        api.Setup(a => a.GetBestScores(mix, It.IsAny<HttpClient>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PiuGameGetBestScoresResult { TotalCharts = bestScoreTotal });
        api.Setup(a => a.GetPumbility(mix, It.IsAny<HttpClient>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PiuGameGetPumbilityResult { Total = pumbility });
        return api;
    }

    private static OfficialSiteClient Client(Mock<IPiuGameApi> api, Mock<IMediator>? mediator = null,
        Mock<IScoreReader>? scores = null)
    {
        return new OfficialSiteClient(api.Object, Mock.Of<IChartRepository>(),
            NullLogger<OfficialSiteClient>.Instance, (mediator ?? new Mock<IMediator>()).Object,
            Mock.Of<ICurrentUserAccessor>(), (scores ?? new Mock<IScoreReader>()).Object,
            Mock.Of<IFileUploadClient>(), Mock.Of<IBus>(), FakeDateTime.At(Now).Object,
            Mock.Of<IDailyStepReader>(), Options.Create(new PiuGameConfiguration()));
    }
}
