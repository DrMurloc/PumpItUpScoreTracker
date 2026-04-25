using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ScoreTracker.Application.Commands;
using ScoreTracker.Application.Handlers;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class SkillsSagaTests
{
    [Fact]
    public async Task GetChartSkillsDelegatesToRepository()
    {
        var skills = new List<ChartSkillsRecord>();
        var charts = new Mock<IChartRepository>();
        charts.Setup(c => c.GetChartSkills(It.IsAny<CancellationToken>())).ReturnsAsync(skills);

        var saga = new SkillsSaga(charts.Object);
        var result = await saga.Handle(new GetChartSkillsQuery(), CancellationToken.None);

        Assert.Same(skills, result);
    }

    [Fact]
    public async Task UpdateChartSkillDelegatesToRepository()
    {
        var record = new ChartSkillsRecord(Guid.NewGuid(),
            new[] { Skill.Twists }, new[] { Skill.Twists });
        var charts = new Mock<IChartRepository>();

        var saga = new SkillsSaga(charts.Object);
        await saga.Handle(new UpdateChartSkillCommand(record), CancellationToken.None);

        charts.Verify(c => c.SaveChartSkills(record, It.IsAny<CancellationToken>()), Times.Once);
    }
}
