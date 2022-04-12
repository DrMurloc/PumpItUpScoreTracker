using System;
using System.Threading;
using System.Threading.Tasks;
using AutoFixture;
using FakeItEasy;
using ScoreTracker.Application.Commands;
using ScoreTracker.Application.Handlers;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Tests.Helpers;
using Xunit;

namespace ScoreTracker.Tests.HandlerTests;

public sealed class RecordAttemptHandlerTests
{
    private readonly Fixture _fixture = FixtureBuilder.Build();

    [Fact]
    public async Task RecordingBetterAttemptSavesNewAttempt()
    {
        //Test Data
        var oldAttempt = new ChartAttempt(LetterGrade.F, true);
        const LetterGrade newLetterGrade = LetterGrade.SSS;
        const bool newIsBroken = false;

        var chart = _fixture.Create<Chart>();
        var now = _fixture.Create<DateTimeOffset>();
        var userId = _fixture.Create<Guid>();

        //Setup
        var dateTimeOffset = A.Fake<IDateTimeOffsetAccessor>();

        A.CallTo(() => dateTimeOffset.Now).Returns(now);

        var user = A.Fake<ICurrentUserAccessor>();

        A.CallTo(() => user.UserId).Returns(userId);

        var repository = A.Fake<IChartAttemptRepository>();

        A.CallTo(() => repository.GetBestAttempt(userId, A<Chart>.Ignored, A<CancellationToken>.Ignored))
            .Returns(oldAttempt);

        var handler = new RecordAttemptHandler(repository, user, dateTimeOffset);

        //Test

        await handler.Handle(
            new RecordAttemptCommand(chart.SongName, chart.Level, chart.Type, newLetterGrade, newIsBroken),
            CancellationToken.None);

        //Assert

        A.CallTo(() => repository.SetBestAttempt(userId, chart,
                A<ChartAttempt>.That.Matches(c => c.LetterGrade == newLetterGrade && c.IsBroken == newIsBroken), now,
                A<CancellationToken>.Ignored))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task RecordingWorseAttemptDoesNotSaveNewAttempt()
    {
        //Test Data
        var oldAttempt = new ChartAttempt(LetterGrade.SSS, false);
        const LetterGrade newLetterGrade = LetterGrade.F;
        const bool newIsBroken = true;

        var chart = _fixture.Create<Chart>();
        var now = _fixture.Create<DateTimeOffset>();
        var userId = _fixture.Create<Guid>();

        //Setup
        var dateTimeOffset = A.Fake<IDateTimeOffsetAccessor>();

        A.CallTo(() => dateTimeOffset.Now).Returns(now);

        var user = A.Fake<ICurrentUserAccessor>();

        A.CallTo(() => user.UserId).Returns(userId);

        var repository = A.Fake<IChartAttemptRepository>();

        A.CallTo(() => repository.GetBestAttempt(userId, A<Chart>.Ignored, A<CancellationToken>.Ignored))
            .Returns(oldAttempt);

        var handler = new RecordAttemptHandler(repository, user, dateTimeOffset);

        //Test

        await handler.Handle(
            new RecordAttemptCommand(chart.SongName, chart.Level, chart.Type, newLetterGrade, newIsBroken),
            CancellationToken.None);

        //Assert

        A.CallTo(() => repository.SetBestAttempt(userId, chart,
                A<ChartAttempt>.That.Matches(c => c.LetterGrade == newLetterGrade && c.IsBroken == newIsBroken), now,
                A<CancellationToken>.Ignored))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task RecordingAttemptWhenNoPreviousExistsSavesNewAttempt()
    {
        //Test Data
        ChartAttempt? oldAttempt = null;
        const LetterGrade newLetterGrade = LetterGrade.SSS;
        const bool newIsBroken = false;

        var chart = _fixture.Create<Chart>();
        var now = _fixture.Create<DateTimeOffset>();
        var userId = _fixture.Create<Guid>();

        //Setup
        var dateTimeOffset = A.Fake<IDateTimeOffsetAccessor>();

        A.CallTo(() => dateTimeOffset.Now).Returns(now);

        var user = A.Fake<ICurrentUserAccessor>();

        A.CallTo(() => user.UserId).Returns(userId);

        var repository = A.Fake<IChartAttemptRepository>();

        A.CallTo(() => repository.GetBestAttempt(userId, A<Chart>.Ignored, A<CancellationToken>.Ignored))
            .Returns(oldAttempt);

        var handler = new RecordAttemptHandler(repository, user, dateTimeOffset);

        //Test

        await handler.Handle(
            new RecordAttemptCommand(chart.SongName, chart.Level, chart.Type, newLetterGrade, newIsBroken),
            CancellationToken.None);

        //Assert

        A.CallTo(() => repository.SetBestAttempt(userId, chart,
                A<ChartAttempt>.That.Matches(c => c.LetterGrade == newLetterGrade && c.IsBroken == newIsBroken), now,
                A<CancellationToken>.Ignored))
            .MustHaveHappenedOnceExactly();
    }
}