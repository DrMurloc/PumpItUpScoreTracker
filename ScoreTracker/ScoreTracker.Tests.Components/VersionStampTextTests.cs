using System;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Web.Services;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The one way a patch prints, shared by the chart page's fact strip and the dialog's
///     History lines: a numbered version takes the v, a named one does not, and the date
///     follows only when the catalog has it.
/// </summary>
public sealed class VersionStampTextTests
{
    [Fact]
    public void ANumberedPatchWithADatePrintsBoth()
    {
        var stamp = new VersionStamp(MixEnum.Phoenix, "2.06.0", new DateOnly(2024, 12, 26), 160);

        Assert.Equal("v2.06.0 · Dec 26, 2024", VersionStampText.Short(stamp));
    }

    [Fact]
    public void ANumberedPatchWithoutADatePrintsTheVersionAlone()
    {
        Assert.Equal("v1.06.0", VersionStampText.Short(new VersionStamp(MixEnum.Prime, "1.06.0", null, 60)));
    }

    [Theory]
    [InlineData("Release", "Release · Sep 3, 2000")]
    [InlineData("Pre-v1.10", "Pre-v1.10 · Sep 3, 2000")]
    public void ANamedVersionPrintsWithoutTheV(string name, string expected)
    {
        var stamp = new VersionStamp(MixEnum.ObgSeasonEvolution, name, new DateOnly(2000, 9, 3), 1);

        Assert.Equal(expected, VersionStampText.Short(stamp));
    }
}
