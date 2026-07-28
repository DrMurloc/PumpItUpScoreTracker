using System;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ScoreTracker.PlayerProgress.Application;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.PlayerProgress.Contracts.Queries;
using ScoreTracker.PlayerProgress.Domain;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class FolderLevelSagaTests
{
    [Fact]
    public async Task TheQueryReturnsTheStoredStandings()
    {
        var userId = Guid.NewGuid();
        var stored = new[]
        {
            new FolderLevelRecord(MixEnum.Phoenix, ChartType.Single, DifficultyLevel.From(22), 97, 90, 934245)
        };
        var folderLevels = new Mock<IPlayerFolderLevelRepository>();
        folderLevels.Setup(f => f.GetFolderLevels(MixEnum.Phoenix, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(stored);
        var saga = new FolderLevelSaga(folderLevels.Object);

        var result = await saga.Handle(new GetPlayerFolderLevelsQuery(userId), CancellationToken.None);

        var folder = Assert.Single(result);
        Assert.Equal("S22", folder.Folder);
        Assert.Equal(92, folder.CompletionPercent);
    }
}
