using System;
using System.Collections.Generic;
using System.Linq;
using ScoreTracker.Domain.Models;
using ScoreTracker.OfficialMirror.Domain;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

/// <summary>
///     The completeness check's whole detection rule: reduce piugame's play-data census and our
///     stored records to the same shape, then subtract. Everything here is pure — the network and
///     the database live on the other side of this boundary.
/// </summary>
public sealed class CensusDiffTests
{
    private static readonly string[] PhoenixBuckets =
        { "", "10", "11", "18", "25", "26", "27over", "10over", "coop" };

    private static readonly string[] Phoenix2Buckets =
        { "", "1", "9", "12", "17", "26", "27over", "10over", "coop" };

    // ---- bucketing ----

    [Theory]
    [InlineData(ChartType.Single, 18, "18")]
    [InlineData(ChartType.Double, 25, "25")]
    [InlineData(ChartType.CoOp, 18, "coop")]
    [InlineData(ChartType.Single, 28, "27over")]
    [InlineData(ChartType.Single, 5, "sub10")]
    public void ChartsFallIntoTheBucketPhoenixWouldCountThemIn(ChartType type, int level, string expected)
    {
        Assert.Equal(expected, CensusBuckets.For(type, DifficultyLevel.From(level), PhoenixBuckets));
    }

    [Fact]
    public void Phoenix2BucketsSubTenLevelsProperlyBecauseItsPageOffersThem()
    {
        // Phoenix's play-data page starts at level 10 and says so; Phoenix 2's reaches level 1, so
        // the same chart is a real bucket there and a residual here.
        Assert.Equal("9", CensusBuckets.For(ChartType.Single, DifficultyLevel.From(9), Phoenix2Buckets));
        Assert.Equal("sub10", CensusBuckets.For(ChartType.Single, DifficultyLevel.From(9), PhoenixBuckets));
    }

    [Fact]
    public void AggregateBucketsAreNotPartOfThePartition()
    {
        // "" is the whole page and "10over" is every numeric bucket summed — counting either as a
        // level would double every chart in the account.
        Assert.True(CensusBuckets.IsAggregate(""));
        Assert.True(CensusBuckets.IsAggregate("10over"));
        Assert.False(CensusBuckets.IsAggregate("27over"));
        Assert.False(CensusBuckets.IsAggregate("coop"));
        Assert.DoesNotContain("10over", CensusBuckets.Partitioning(PhoenixBuckets));
        Assert.Contains("27over", CensusBuckets.Partitioning(PhoenixBuckets));
    }

    // ---- our side ----

    [Fact]
    public void OurCensusCountsPassesOnlySoABreakImporterIsNotPermanentlyAhead()
    {
        var charts = Charts(("a", ChartType.Single, 18), ("b", ChartType.Single, 18), ("c", ChartType.Single, 18));
        var records = new[]
        {
            Record(charts, "a", 990000, PhoenixPlate.MarvelousGame),
            Record(charts, "b", 400000, PhoenixPlate.RoughGame, broken: true),
            new RecordedPhoenixScore(Id("c"), null, null, false, DateTimeOffset.UnixEpoch)
        };

        var census = LocalCensusBuilder.Build(MixEnum.Phoenix, records, charts, PhoenixBuckets);

        // The play-data page has no notion of a break, and a scoreless row is a manual placeholder
        // with no counterpart there either.
        Assert.Equal(1, census.For("18").Passes);
    }

    [Fact]
    public void OurCensusReDerivesGradesForTheMixBeingChecked()
    {
        // 905,000 is an A+ under Phoenix's floors and an A under Phoenix 2's — the stored
        // LetterGrade belongs to whichever mix wrote the row, so it is never trusted here.
        var charts = Charts(("a", ChartType.Single, 18));
        var records = new[] { Record(charts, "a", 905000, PhoenixPlate.FairGame) };

        var phoenix = LocalCensusBuilder.Build(MixEnum.Phoenix, records, charts, PhoenixBuckets);
        var phoenix2 = LocalCensusBuilder.Build(MixEnum.Phoenix2, records, charts, PhoenixBuckets);

        Assert.NotEqual(
            phoenix.For("18").Grades.Single().Key,
            phoenix2.For("18").Grades.Single().Key);
    }

    [Fact]
    public void OurPumbilityIsTheMergedTopFiftyWithBreaksAndCoOpExcluded()
    {
        var charts = Charts(("a", ChartType.Single, 20), ("b", ChartType.Double, 20),
            ("c", ChartType.CoOp, 20), ("d", ChartType.Single, 20));
        var records = new[]
        {
            Record(charts, "a", 990000, PhoenixPlate.MarvelousGame),
            Record(charts, "b", 990000, PhoenixPlate.MarvelousGame),
            Record(charts, "c", 990000, PhoenixPlate.MarvelousGame),
            Record(charts, "d", 990000, PhoenixPlate.MarvelousGame, broken: true)
        };

        var all = LocalCensusBuilder.Pumbility(MixEnum.Phoenix, records, charts);
        var singleOnly = LocalCensusBuilder.Pumbility(MixEnum.Phoenix,
            new[] { records[0] }, charts);

        // CO-OP and broken plays never rate, so four records price as two — mirroring
        // PlayerRatingSaga, which is why the panel's number agrees with the PUMBILITY page.
        Assert.Equal(singleOnly * 2, all, 3);
    }

    [Fact]
    public void OurPumbilityIgnoresRecordsForChartsTheMixDoesNotHave()
    {
        var charts = Charts(("a", ChartType.Single, 20));
        var records = new[]
        {
            Record(charts, "a", 990000, PhoenixPlate.MarvelousGame),
            // A record whose chart is not in this mix's catalog — a cross-mix row, not a zero.
            new RecordedPhoenixScore(Id("ff"), PhoenixScore.From(999000), PhoenixPlate.PerfectGame, false,
                DateTimeOffset.UnixEpoch)
        };

        Assert.Equal(LocalCensusBuilder.Pumbility(MixEnum.Phoenix, new[] { records[0] }, charts),
            LocalCensusBuilder.Pumbility(MixEnum.Phoenix, records, charts), 3);
    }

    // ---- the diff ----

    [Fact]
    public void ABucketPiuGameHasMorePassesInReadsAsMissingScores()
    {
        var official = Census(("18", 366), ("21", 227));
        var local = Census(("18", 365), ("21", 225));

        var findings = CensusDiff.Compare(official, local);

        Assert.Equal(2, findings.Count);
        Assert.Equal(new[] { ("18", 1), ("21", 2) },
            findings.Select(f => (f.Bucket, f.Count)).ToArray());
        Assert.All(findings, f => Assert.Equal(CensusFindingKind.Missing, f.Kind));
    }

    [Fact]
    public void HoldingMoreThanPiuGameIsReportedButIsNotAnError()
    {
        var findings = CensusDiff.Compare(Census(("18", 10)), Census(("18", 12)));

        var finding = Assert.Single(findings);
        Assert.Equal(CensusFindingKind.Extra, finding.Kind);
        Assert.Equal(2, finding.Count);
        // A CSV import or a retired chart is never re-read: there is nothing to fetch.
        Assert.Empty(CensusDiff.BucketsToRepair(findings));
    }

    [Fact]
    public void MatchingTotalsAcrossDifferentBucketsIsStillAFinding()
    {
        // The result that shaped the design: on a real 2,851-chart Phoenix account the whole
        // account total matched exactly while level 18 was short one and sub-10 was long one.
        var official = Census(("18", 366), ("sub10", 75));
        var local = Census(("18", 365), ("sub10", 76));

        Assert.Equal(official.TotalPasses, local.TotalPasses);
        var findings = CensusDiff.Compare(official, local);

        Assert.Equal(CensusFindingKind.Missing, findings.Single(f => f.Bucket == "18").Kind);
        Assert.Equal(CensusFindingKind.Extra, findings.Single(f => f.Bucket == "sub10").Kind);
    }

    [Fact]
    public void AShiftedGradeSpreadAtMatchingTotalsReadsAsOutOfDateScores()
    {
        var official = Census(("18", 3, Grades(("AA", 2), ("A_PLUS", 1))));
        var local = Census(("18", 3, Grades(("AA", 1), ("A_PLUS", 2))));

        var finding = Assert.Single(CensusDiff.Compare(official, local));

        Assert.Equal(CensusFindingKind.OutOfDate, finding.Kind);
        Assert.Equal("AA", finding.Band);
        Assert.True(finding.IsGradeBand);
        Assert.Equal(1, finding.Count);
    }

    [Fact]
    public void PhoenixFallsBackToPlateBandsBecauseItPublishesNoGrades()
    {
        var official = Census(("18", 2, Grades(), Plates(("mg", 2))));
        var local = Census(("18", 2, Grades(), Plates(("mg", 1), ("tg", 1))));

        var finding = Assert.Single(CensusDiff.Compare(official, local));

        Assert.Equal("mg", finding.Band);
        Assert.False(finding.IsGradeBand);
    }

    [Fact]
    public void AShortBucketDoesNotAlsoReportItsBandsAsOutOfDate()
    {
        // While a bucket is missing charts its bands are short too. Reporting both would count the
        // same charts twice under two different names and inflate the repair.
        var official = Census(("18", 3, Grades(("AA", 3))));
        var local = Census(("18", 1, Grades(("AA", 1))));

        var finding = Assert.Single(CensusDiff.Compare(official, local));

        Assert.Equal(CensusFindingKind.Missing, finding.Kind);
        Assert.Null(finding.Band);
    }

    [Fact]
    public void AnAgreeingCensusFindsNothing()
    {
        var census = Census(("18", 366, Grades(("AA", 366))));

        Assert.Empty(CensusDiff.Compare(census, census));
        Assert.Empty(CensusDiff.BucketsToRepair(CensusDiff.Compare(census, census)));
    }

    // ---- what a repair re-reads ----

    [Fact]
    public void OnlyTheBucketsThatCouldGainSomethingAreReRead()
    {
        var findings = new[]
        {
            new CensusFinding("18", CensusFindingKind.Missing, 1),
            new CensusFinding("21", CensusFindingKind.OutOfDate, 1, "AA", true),
            new CensusFinding("24", CensusFindingKind.Extra, 3)
        };

        // There is nothing to fetch for a level where we already hold more than piugame.
        Assert.Equal(new[] { "18", "21" }, CensusDiff.BucketsToRepair(findings));
    }

    [Fact]
    public void ABucketWithTwoFindingsIsOnlyReadOnce()
    {
        var findings = new[]
        {
            new CensusFinding("18", CensusFindingKind.OutOfDate, 1, "AA", true),
            new CensusFinding("18", CensusFindingKind.OutOfDate, 2, "SS", true)
        };

        // A repair reads a whole level, never a band: the count tile's modal names charts but
        // carries no score, so the best-score list — which filters by level and nothing finer —
        // is the only surface a repair can save from.
        Assert.Equal(new[] { "18" }, CensusDiff.BucketsToRepair(findings));
    }

    [Fact]
    public void TheSubTenResidualIsNotRepairable()
    {
        var findings = new[] { new CensusFinding("sub10", CensusFindingKind.Missing, 1) };

        // Phoenix will not filter its best list below level 10 either, so those charts can only
        // be reached by reading the whole list — a deep scan, not a localised repair.
        Assert.Empty(CensusDiff.BucketsToRepair(findings));
    }

    // ---- builders ----

    private static Guid Id(string key)
    {
        return new Guid(key.PadRight(32, '0'));
    }

    private static Dictionary<Guid, Chart> Charts(params (string Key, ChartType Type, int Level)[] charts)
    {
        return charts.ToDictionary(c => Id(c.Key),
            c => new ChartBuilder().WithId(Id(c.Key)).WithType(c.Type).WithLevel(c.Level).Build());
    }

    private static RecordedPhoenixScore Record(IReadOnlyDictionary<Guid, Chart> charts, string key, int score,
        PhoenixPlate plate, bool broken = false)
    {
        return new RecordedPhoenixScore(Id(key), PhoenixScore.From(score), plate, broken, DateTimeOffset.UnixEpoch);
    }

    private static Dictionary<string, int> Grades(params (string Band, int Count)[] bands)
    {
        return bands.ToDictionary(b => b.Band, b => b.Count, StringComparer.Ordinal);
    }

    private static Dictionary<string, int> Plates(params (string Band, int Count)[] bands)
    {
        return bands.ToDictionary(b => b.Band, b => b.Count, StringComparer.Ordinal);
    }

    private static AccountCensus Census(params (string Bucket, int Passes)[] buckets)
    {
        return new AccountCensus(MixEnum.Phoenix,
            buckets.ToDictionary(b => b.Bucket,
                b => new CensusBucket(b.Bucket, b.Passes, Grades(), Plates()), StringComparer.Ordinal), 0);
    }

    private static AccountCensus Census(params (string Bucket, int Passes, Dictionary<string, int> Grades)[] buckets)
    {
        return new AccountCensus(MixEnum.Phoenix,
            buckets.ToDictionary(b => b.Bucket,
                b => new CensusBucket(b.Bucket, b.Passes, b.Grades, Plates()), StringComparer.Ordinal), 0);
    }

    private static AccountCensus Census(
        params (string Bucket, int Passes, Dictionary<string, int> Grades, Dictionary<string, int> Plates)[] buckets)
    {
        return new AccountCensus(MixEnum.Phoenix,
            buckets.ToDictionary(b => b.Bucket,
                b => new CensusBucket(b.Bucket, b.Passes, b.Grades, b.Plates), StringComparer.Ordinal), 0);
    }
}
