using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ScoreTracker.Catalog.Contracts.Commands;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.Catalog.Domain;
using ScoreTracker.ChartIntelligence.Contracts.Commands;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.Application.Commands;
using ScoreTracker.Catalog.Application;
using ScoreTracker.Application.Queries;
using ScoreTracker.SharedKernel.Enums;
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

        var saga = new SkillsSaga(charts.Object, Mock.Of<IChartSkillMetricRepository>(),
            Mock.Of<IExternalChartAliasRepository>());
        var result = await saga.Handle(new GetChartSkillsQuery(), CancellationToken.None);

        Assert.Same(skills, result);
    }

    [Fact]
    public async Task UpdateChartSkillDelegatesToRepository()
    {
        var record = new ChartSkillsRecord(Guid.NewGuid(),
            new[] { Skill.Twists }, new[] { Skill.Twists });
        var charts = new Mock<IChartRepository>();

        var saga = new SkillsSaga(charts.Object, Mock.Of<IChartSkillMetricRepository>(),
            Mock.Of<IExternalChartAliasRepository>());
        await saga.Handle(new UpdateChartSkillCommand(record), CancellationToken.None);

        charts.Verify(c => c.SaveChartSkills(record, It.IsAny<CancellationToken>()), Times.Once);
    }
}
