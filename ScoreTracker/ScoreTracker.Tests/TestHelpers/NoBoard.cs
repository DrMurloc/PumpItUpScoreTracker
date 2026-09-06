using System;
using System.Collections.Generic;
using System.Threading;
using Moq;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Tests.TestHelpers;

/// <summary>
///     A mirror with nothing on it: no board peers and no board scores, so a projection is exactly
///     the site's own peers. Every suite that is not measuring the board half reads this, which is
///     what keeps "did the board change this" answerable by looking at one stub.
/// </summary>
internal static class NoBoard
{
    public static IOfficialPlacementReader Reader => Mock().Object;

    public static Mock<IOfficialPlacementReader> Mock()
    {
        var reader = new Mock<IOfficialPlacementReader>();
        reader.Setup(r => r.GetBoardPeers(It.IsAny<MixEnum>(), It.IsAny<ChartType>(), It.IsAny<double>(),
                It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BoardPeerGroupReading?)null);
        reader.Setup(r => r.GetBoardScores(It.IsAny<MixEnum>(), It.IsAny<ChartType>(),
                It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<BoardScoreReading>());
        return reader;
    }
}
