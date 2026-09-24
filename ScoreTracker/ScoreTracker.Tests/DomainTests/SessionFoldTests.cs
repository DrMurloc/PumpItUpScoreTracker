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
            .Select(i => Import(Evening.AddMinutes(i * 12), Evening.AddMinutes(i * 12 + 10)))
            .ToArray();

        var session = Assert.Single(SessionFold.Fold(imports));

        Assert.Equal(imports.Select(i => i.SessionId!.Value), session.SessionIds);
    }

    [Fact]
    public void EightHoursWithoutAnImportEndASession()
    {
        var night = Import(Evening, Evening.AddHours(2));
        var nextMorning = Import(Evening.AddHours(10).AddMinutes(1), Evening.AddHours(11));

        Assert.Equal(2, SessionFold.Fold(new[] { night, nextMorning }).Count);
    }

    [Fact]
    public void ExactlyEightHoursApartIsStillOneSession()
    {
        // The envelope extends at exactly eight hours too; the two sides must end sessions alike.
        var first = Import(Evening, Evening.AddHours(1));
        var second = Import(Evening.AddHours(9), Evening.AddHours(10));

        Assert.Single(SessionFold.Fold(new[] { first, second }));
    }

    [Fact]
    public void TheGapIsMeasuredFromTheLatestImportActivityTheSessionHolds()
    {
        // A long import that runs past a short one: the third starts more than eight hours after
        // the short one finished but within eight of the long one, so the session is still open.
        var longRun = Import(Evening, Evening.AddHours(6));
        var shortRun = Import(Evening.AddHours(1), Evening.AddHours(1).AddMinutes(20));
        var late = Import(Evening.AddHours(13), Evening.AddHours(14));

        var session = Assert.Single(SessionFold.Fold(new[] { longRun, shortRun, late }));

        Assert.Equal(3, session.SessionIds.Count);
    }

    [Fact]
    public void AnOldDatedPlayInAnImportPullsNoOtherNightIn()
    {
        // The bug check's shape: an import run on Sep 5 carrying a stage break the site dated to
        // the chart's first attempt, Aug 9. By play time it covered Aug 9 to Sep 5 and swallowed
        // every night between; by import time it is Sep 5, and the Aug 20 night stays its own.
        var sep5 = new DateTimeOffset(2026, 9, 5, 21, 0, 0, TimeSpan.Zero);
        var aug20 = new DateTimeOffset(2026, 8, 20, 21, 0, 0, TimeSpan.Zero);
        var withOldPlay = Import(sep5, sep5.AddMinutes(10), firstPlay: new DateTimeOffset(2026, 8, 9, 20, 0, 0,
            TimeSpan.Zero), lastPlay: sep5);
        var midMonth = Import(aug20, aug20.AddMinutes(10), firstPlay: aug20.AddHours(-2), lastPlay: aug20);

        var sessions = SessionFold.Fold(new[] { withOldPlay, midMonth });

        Assert.Equal(2, sessions.Count);
    }

    [Fact]
    public void ImportsGroupByWhenTheyRanWhateverTheirPlaysSay()
    {
        // Monday night's plays imported Tuesday at 09:00, then Tuesday's at 12:00: two imports three
        // hours apart are one session, though the plays in them are a night apart.
        var monday = new DateTimeOffset(2026, 9, 21, 20, 0, 0, TimeSpan.Zero);
        var tuesday9 = monday.AddHours(13);
        var lastNight = Import(tuesday9, tuesday9.AddMinutes(5), firstPlay: monday, lastPlay: monday.AddHours(2));
        var thisMorning = Import(tuesday9.AddHours(3), tuesday9.AddHours(3).AddMinutes(5),
            firstPlay: tuesday9.AddHours(2), lastPlay: tuesday9.AddHours(3));

        var session = Assert.Single(SessionFold.Fold(new[] { lastNight, thisMorning }));

        // The page still orders and prints a session by its plays' own dates.
        Assert.Equal(monday, session.Start);
        Assert.Equal(tuesday9.AddHours(3), session.End);
    }

    [Fact]
    public void AStoredSessionWithNoImportTimeIsNeverGrouped()
    {
        // No import time recorded — a CSV upload, an API request, anything before the table shipped
        // — means nothing to group by, and nothing is backfilled from play dates: each stands alone,
        // as it always did, even minutes from its neighbours.
        var upload = new StoredSessionKey(Guid.NewGuid(), null, MixEnum.Phoenix2, Evening, Evening.AddMinutes(5));
        var request = new StoredSessionKey(Guid.NewGuid(), null, MixEnum.Phoenix2, Evening.AddMinutes(10),
            Evening.AddMinutes(11));
        var import = Import(Evening.AddMinutes(20), Evening.AddMinutes(30));

        var sessions = SessionFold.Fold(new[] { upload, request, import });

        Assert.Equal(3, sessions.Count);
        Assert.All(sessions, s => Assert.Single(s.SessionIds));
    }

    [Fact]
    public void APreCaptureDayStandsAloneAndKeepsNoId()
    {
        var day = new StoredSessionKey(null, new DateOnly(2026, 6, 10), MixEnum.Phoenix,
            new DateTimeOffset(2026, 6, 10, 22, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 6, 10, 23, 50, 0, TimeSpan.Zero));

        var session = Assert.Single(SessionFold.Fold(new[] { day }));

        Assert.Null(session.SessionId);
        Assert.Empty(session.SessionIds);
        Assert.Equal(new DateOnly(2026, 6, 10), session.Day);
    }

    [Fact]
    public void AMixIsItsOwnTimeline()
    {
        // Phoenix and Phoenix 2 imported minutes apart are two sessions: mix is the one thing that
        // splits a night (D53).
        var phoenix = Import(Evening, Evening.AddMinutes(10), MixEnum.Phoenix);
        var phoenix2 = Import(Evening.AddMinutes(20), Evening.AddMinutes(30));

        var sessions = SessionFold.Fold(new[] { phoenix, phoenix2 });

        Assert.Equal(2, sessions.Count);
        Assert.Contains(sessions, s => s.Mix == MixEnum.Phoenix && s.SessionId == phoenix.SessionId);
        Assert.Contains(sessions, s => s.Mix == MixEnum.Phoenix2 && s.SessionId == phoenix2.SessionId);
    }

    [Fact]
    public void TheHandleIsTheNewestImportAndEveryIdTravelsOldestFirst()
    {
        var first = Import(Evening, Evening.AddMinutes(30));
        var last = Import(Evening.AddHours(2), Evening.AddHours(3));
        var middle = Import(Evening.AddHours(1), Evening.AddHours(1).AddMinutes(30));

        var session = Assert.Single(SessionFold.Fold(new[] { last, first, middle }));

        Assert.Equal(last.SessionId, session.SessionId);
        Assert.Equal(new[] { first.SessionId!.Value, middle.SessionId!.Value, last.SessionId!.Value },
            session.SessionIds);
        Assert.Null(session.Day);
    }

    [Fact]
    public void NoKeysAreNoSessions()
    {
        Assert.Empty(SessionFold.Fold(Array.Empty<StoredSessionKey>()));
    }

    /// <summary>
    ///     A stored session with an import window. Its plays default to the window itself; the
    ///     cases about old-dated plays set them apart.
    /// </summary>
    private static StoredSessionKey Import(DateTimeOffset started, DateTimeOffset lastActivity,
        MixEnum mix = MixEnum.Phoenix2, DateTimeOffset? firstPlay = null, DateTimeOffset? lastPlay = null)
    {
        return new StoredSessionKey(Guid.NewGuid(), null, mix, firstPlay ?? started, lastPlay ?? lastActivity,
            new ImportWindow(started, lastActivity));
    }
}
