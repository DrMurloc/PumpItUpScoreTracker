using System;
using System.Collections.Generic;
using System.Linq;
using ScoreTracker.Catalog.Contracts;
using ScoreTracker.SharedKernel.Enums;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

/// <summary>
///     The one place "through this version" is spelled (docs/design/chart-versions.md §3): the API,
///     the /Charts drawer and the randomizer all resolve their version questions here, so a
///     disagreement between them is impossible by construction.
/// </summary>
public sealed class MixVersionRangeTests
{
    private static readonly IReadOnlyList<MixVersionRecord> Phoenix = new[]
    {
        new MixVersionRecord(MixEnum.Phoenix, "1.00.0", new DateOnly(2023, 7, 4), 10, 300),
        new MixVersionRecord(MixEnum.Phoenix, "1.01.0", new DateOnly(2023, 7, 27), 20, 53),
        new MixVersionRecord(MixEnum.Phoenix, "2.00.0", new DateOnly(2024, 5, 27), 30, 107),
        new MixVersionRecord(MixEnum.Phoenix, "JE", null, 40, 42)
    };

    private static IReadOnlySet<string>? Resolve(IReadOnlyCollection<string>? inVersions = null, string? by = null,
        string? after = null, DateOnly? releasedAfter = null)
    {
        Assert.True(MixVersionRange.TryResolve(Phoenix, inVersions, by, after, releasedAfter, out var names, out var unknown));
        Assert.Null(unknown);
        return names;
    }

    [Fact]
    public void NoFilterResolvesToNoFilter()
    {
        Assert.Null(Resolve());
    }

    [Fact]
    public void InVersionsKeepsExactlyThoseNamesAndTrimsThem()
    {
        var names = Resolve(inVersions: new[] { " 1.01.0", "JE " });

        Assert.Equal(new[] { "1.01.0", "JE" }, names!.OrderBy(n => n));
    }

    [Fact]
    public void ByVersionIsUpToAndIncluding()
    {
        var names = Resolve(by: "1.01.0");

        Assert.Equal(new[] { "1.00.0", "1.01.0" }, names!.OrderBy(n => n));
    }

    [Fact]
    public void AfterVersionIsStrictlyAfter()
    {
        var names = Resolve(after: "1.01.0");

        Assert.Equal(new[] { "2.00.0", "JE" }, names!.OrderBy(n => n));
    }

    [Fact]
    public void ByAndAfterCombineIntoARange()
    {
        var names = Resolve(by: "2.00.0", after: "1.00.0");

        Assert.Equal(new[] { "1.01.0", "2.00.0" }, names!.OrderBy(n => n));
    }

    [Fact]
    public void ReleasedAfterIsExclusiveAndSkipsUndatedPatches()
    {
        var names = Resolve(releasedAfter: new DateOnly(2023, 7, 27));

        Assert.Equal(new[] { "2.00.0" }, names!);
    }

    [Fact]
    public void AnInVersionOutsideByVersionResolvesToNothingRatherThanFailing()
    {
        var names = Resolve(inVersions: new[] { "2.00.0" }, by: "1.01.0");

        Assert.Empty(names!);
    }

    [Theory]
    [InlineData("in")]
    [InlineData("by")]
    [InlineData("after")]
    public void AnUnknownNameFailsAndNamesTheOffender(string where)
    {
        var ok = MixVersionRange.TryResolve(Phoenix,
            where == "in" ? new[] { "1.00.0", "9.99.9" } : null,
            where == "by" ? "9.99.9" : null,
            where == "after" ? "9.99.9" : null,
            null, out var names, out var unknown);

        Assert.False(ok);
        Assert.Null(names);
        Assert.Equal("9.99.9", unknown);
    }

    [Fact]
    public void ThroughListsEveryVersionUpToAndIncludingTheNamedOneInOrder()
    {
        Assert.Equal(new[] { "1.00.0", "1.01.0", "2.00.0" }, MixVersionRange.Through(Phoenix, "2.00.0"));
        Assert.Empty(MixVersionRange.Through(Phoenix, "9.99.9"));
    }

    [Fact]
    public void ThroughNameCollapsesAPrefixOfTheOrderAndNothingElse()
    {
        Assert.Equal("1.01.0", MixVersionRange.ThroughName(Phoenix, new[] { "1.01.0", "1.00.0" }));
        Assert.Null(MixVersionRange.ThroughName(Phoenix, new[] { "1.00.0", "2.00.0" }));
        Assert.Null(MixVersionRange.ThroughName(Phoenix, new[] { "1.00.0" }));
        Assert.Null(MixVersionRange.ThroughName(Phoenix, Array.Empty<string>()));
    }
}
