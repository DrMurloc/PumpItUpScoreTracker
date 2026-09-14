using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Moq;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Tests.TestHelpers;

/// <summary>
///     A season calendar for tests. <see cref="None" /> is the state every suite that is not about
///     seasons wants — the roll has opened nothing, so no seasonal row can exist and the code under
///     test behaves exactly as it did before seasons.
/// </summary>
internal static class FakeSeasons
{
    public static Mock<ISeasonReader> None()
    {
        return Of();
    }

    /// <summary>Quarters, built on March of Murlocs' UTC-5 boundary, unsealed unless named in <paramref name="sealedSeasons" />.</summary>
    public static Mock<ISeasonReader> Of(params SeasonId[] seasons)
    {
        return WithSealed(seasons, Array.Empty<SeasonId>());
    }

    public static Mock<ISeasonReader> WithSealed(IReadOnlyList<SeasonId> seasons, IReadOnlyList<SeasonId> sealedSeasons)
    {
        var records = seasons.Select(s => Quarter(s, sealedSeasons.Contains(s))).ToArray();
        var reader = new Mock<ISeasonReader>();
        reader.Setup(r => r.GetSeasons(It.IsAny<CancellationToken>())).ReturnsAsync(records);
        reader.Setup(r => r.GetSeasonAt(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DateTimeOffset at, CancellationToken _) => records.FirstOrDefault(s => s.Holds(at)));
        return reader;
    }

    public static SeasonRecord Quarter(SeasonId id, bool isSealed = false)
    {
        var offset = TimeSpan.FromHours(-5);
        var month = id.Quarter * 3;
        var ends = new DateTimeOffset(new DateTime(id.Year, month, DateTime.DaysInMonth(id.Year, month), 23, 59, 59),
            offset);
        return new SeasonRecord(id, $"Q{id.Quarter} {id.Year}",
            new DateTimeOffset(new DateTime(id.Year, month - 2, 1, 0, 0, 0), offset), ends,
            isSealed ? ends.AddDays(7) : null, false);
    }
}
