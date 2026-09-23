using System;
using System.Linq;
using ScoreTracker.ScoreLedger.Domain;
using ScoreTracker.SharedKernel.Enums;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public sealed class SessionFoldTests
{
    private static readonly DateTimeOffset Evening = new(2026, 9, 23, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public void FiveImportsInAnHourAreOneSession()
    {
        // The owner's case: each import stores its own session, and each one fired its own
        // Discord card, but it is one night of play.
        var imports = Enumerable.Range(0, 5)
            .Select(i => Stored(Evening.AddMinutes(i * 12), Evening.AddMinutes(i * 12 + 10)))
            .ToArray();

        var session = Assert.Single(SessionFold.Fold(imports));

        Assert.Equal(imports.Select(i => i.SessionId!.Value), session.SessionIds);
        Assert.Equal(Evening, session.Start);
        Assert.Equal(Evening.AddMinutes(58), session.End);
    }

    [Fact]
    public void EightQuietHoursEndASession()
    {
        var night = Stored(Evening, Evening.AddHours(2));
        var nextMorning = Stored(Evening.AddHours(10).AddMinutes(1), Evening.AddHours(11));

        var sessions = SessionFold.Fold(new[] { night, nextMorning });

        Assert.Equal(2, sessions.Count);
    }

    [Fact]
    public void ExactlyEightHoursApartIsStillOneSession()
    {
        // The envelope extends at exactly eight hours too; the two sides must end sessions alike.
        var first = Stored(Evening, Evening.AddHours(1));
        var second = Stored(Evening.AddHours(9), Evening.AddHours(10));

        Assert.Single(SessionFold.Fold(new[] { first, second }));
    }

    [Fact]
    public void TheGapIsMeasuredFromTheLatestPlayTheSessionHolds()
    {
        // A long import that runs past a short one: the third import is more than eight hours
        // after the short one ended but within eight of the long one, so the night is still open.
        var longRun = Stored(Evening, Evening.AddHours(6));
        var shortRun = Stored(Evening.AddHours(1), Evening.AddHours(1).AddMinutes(20));
        var late = Stored(Evening.AddHours(13), Evening.AddHours(14));

        var session = Assert.Single(SessionFold.Fold(new[] { longRun, shortRun, late }));

        Assert.Equal(3, session.SessionIds.Count);
    }

    [Fact]
    public void OverlappingStoredSessionsFold()
    {
        var early = Stored(Evening, Evening.AddHours(3));
        var overlapping = Stored(Evening.AddHours(1), Evening.AddHours(2));

        var session = Assert.Single(SessionFold.Fold(new[] { early, overlapping }));

        Assert.Equal(Evening.AddHours(3), session.End);
    }

    [Fact]
    public void AMixIsItsOwnTimeline()
    {
        // Phoenix and Phoenix 2 minutes apart are two sessions: mix is the one thing that splits a
        // night (D53).
        var phoenix = Stored(Evening, Evening.AddHours(1), MixEnum.Phoenix);
        var phoenix2 = Stored(Evening.AddMinutes(20), Evening.AddHours(2), MixEnum.Phoenix2);

        var sessions = SessionFold.Fold(new[] { phoenix, phoenix2 });

        Assert.Equal(2, sessions.Count);
        Assert.Contains(sessions, s => s.Mix == MixEnum.Phoenix && s.SessionId == phoenix.SessionId);
        Assert.Contains(sessions, s => s.Mix == MixEnum.Phoenix2 && s.SessionId == phoenix2.SessionId);
    }

    [Fact]
    public void TheHandleIsTheNewestStoredSessionAndEveryIdTravelsOldestFirst()
    {
        var first = Stored(Evening, Evening.AddMinutes(30));
        var last = Stored(Evening.AddHours(2), Evening.AddHours(3));
        var middle = Stored(Evening.AddHours(1), Evening.AddHours(1).AddMinutes(30));

        var session = Assert.Single(SessionFold.Fold(new[] { last, first, middle }));

        Assert.Equal(last.SessionId, session.SessionId);
        Assert.Equal(new[] { first.SessionId!.Value, middle.SessionId!.Value, last.SessionId!.Value },
            session.SessionIds);
        Assert.Null(session.Day);
    }

    [Fact]
    public void PreCaptureDaysFoldByTheSameRuleAndKeepNoId()
    {
        // Rows from before session capture group by calendar day; a late night that crossed
        // midnight is two days and one session.
        var beforeMidnight = Day(new DateTimeOffset(2026, 6, 10, 22, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 6, 10, 23, 50, 0, TimeSpan.Zero));
        var afterMidnight = Day(new DateTimeOffset(2026, 6, 11, 0, 10, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 6, 11, 1, 0, 0, TimeSpan.Zero));

        var session = Assert.Single(SessionFold.Fold(new[] { afterMidnight, beforeMidnight }));

        Assert.Null(session.SessionId);
        Assert.Empty(session.SessionIds);
        Assert.Equal(new DateOnly(2026, 6, 11), session.Day);
        Assert.Equal(2, session.Days.Count);
    }

    [Fact]
    public void ADayBesideAStoredSessionIsHandledByTheStoredSession()
    {
        var day = Day(new DateTimeOffset(2026, 7, 4, 22, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 7, 4, 23, 0, 0, TimeSpan.Zero));
        var firstCaptured = Stored(new DateTimeOffset(2026, 7, 5, 1, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 7, 5, 2, 0, 0, TimeSpan.Zero), MixEnum.Phoenix);

        var session = Assert.Single(SessionFold.Fold(new[] { day, firstCaptured }));

        Assert.Equal(firstCaptured.SessionId, session.SessionId);
        Assert.Null(session.Day);
        Assert.Single(session.Days);
    }

    [Fact]
    public void NoKeysAreNoSessions()
    {
        Assert.Empty(SessionFold.Fold(Array.Empty<StoredSessionKey>()));
    }

    private static StoredSessionKey Stored(DateTimeOffset start, DateTimeOffset end,
        MixEnum mix = MixEnum.Phoenix2)
    {
        return new StoredSessionKey(Guid.NewGuid(), null, mix, start, end);
    }

    private static StoredSessionKey Day(DateTimeOffset start, DateTimeOffset end, MixEnum mix = MixEnum.Phoenix)
    {
        return new StoredSessionKey(null, DateOnly.FromDateTime(start.Date), mix, start, end);
    }
}
