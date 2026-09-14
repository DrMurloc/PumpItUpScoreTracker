using System;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Web.Services;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The one way a patch prints, shared by the chart page's fact strip and the dialog's
///     History events: a numbered version takes the v, a version the catalog only names is the
///     mix's own release and prints nothing of its own, and the date stands where it is known.
/// </summary>
public sealed class VersionStampTextTests
{
    [Fact]
    public void ANumberedPatchWithADatePrintsBoth()
    {
        var stamp = new VersionStamp(MixEnum.Phoenix, "2.06.0", new DateOnly(2024, 12, 26), 160);

        Assert.Equal("v2.06.0", VersionStampText.Patch(stamp));
        Assert.Equal("v2.06.0 · Dec 26, 2024", VersionStampText.Short(stamp));
    }

    [Fact]
    public void ANumberedPatchWithoutADatePrintsTheVersionAlone()
    {
        Assert.Equal("v1.06.0", VersionStampText.Short(new VersionStamp(MixEnum.Prime, "1.06.0", null, 60)));
    }

    [Theory]
    [InlineData("Release")]
    [InlineData("Pre-v1.10")]
    public void ANamedVersionIsNoPatchAndLeavesTheDayAlone(string name)
    {
        var stamp = new VersionStamp(MixEnum.ObgSeasonEvolution, name, new DateOnly(2000, 9, 3), 1);

        Assert.Null(VersionStampText.Patch(stamp));
        Assert.Equal("Sep 3, 2000", VersionStampText.Short(stamp));
    }

    [Fact]
    public void ANamedVersionWithNoDayNamesItselfRatherThanNothing()
    {
        Assert.Equal("Release", VersionStampText.Short(new VersionStamp(MixEnum.Extra, "Release", null, 1)));
    }
}
