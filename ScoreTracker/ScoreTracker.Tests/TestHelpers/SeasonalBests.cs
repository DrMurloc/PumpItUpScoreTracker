using System;
using System.Collections.Generic;
using System.Threading;
using Moq;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.ScoreLedger.Application;
using ScoreTracker.ScoreLedger.Domain;

namespace ScoreTracker.Tests.TestHelpers;

/// <summary>
///     The seasonal half of a personal best, for the suites that are not about seasons.
///     <see cref="Inert" /> hands back a writer whose calendar is empty, which is what the roll has
///     not yet opened a season means — it reads the season list, finds none and writes nothing, so
///     a test of the all-time path is unaffected by its presence.
/// </summary>
internal static class SeasonalBests
{
    public static SeasonalBestWriter Inert(Mock<IPhoenixRecordRepository>? records = null)
    {
        var seasons = new Mock<ISeasonReader>();
        seasons.Setup(s => s.GetSeasons(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<SeasonRecord>());
        return new SeasonalBestWriter((records ?? new Mock<IPhoenixRecordRepository>()).Object, seasons.Object,
            FakeDateTime.At(DateTimeOffset.UnixEpoch).Object);
    }

    /// <summary>A writer over a real calendar, for the tests that ARE about seasons.</summary>
    public static SeasonalBestWriter Over(Mock<IPhoenixRecordRepository> records,
        IReadOnlyList<SeasonRecord> calendar, DateTimeOffset now)
    {
        var seasons = new Mock<ISeasonReader>();
        seasons.Setup(s => s.GetSeasons(It.IsAny<CancellationToken>())).ReturnsAsync(calendar);
        return new SeasonalBestWriter(records.Object, seasons.Object, FakeDateTime.At(now).Object);
    }
}
