using System;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Moq;
using ScoreTracker.Domain.Models;
using ScoreTracker.Web.Accessors;
using ScoreTracker.Web.Security;
using Xunit;

namespace ScoreTracker.Tests.Components;

public sealed class HttpContextUserAccessorTests
{
    private static ClaimsPrincipal Authenticated(Guid userId)
    {
        // A non-null authentication type is what makes Identity.IsAuthenticated true.
        return new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Name, "Tester"),
            new Claim(ScoreTrackerClaimTypes.IsPublic, "true")
        }, "TestAuth"));
    }

    private static Mock<IHttpContextAccessor> ContextWith(ClaimsPrincipal? user)
    {
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.SetupGet(a => a.HttpContext)
            .Returns(user == null ? null : new DefaultHttpContext { User = user });
        return accessor;
    }

    [Fact]
    public void CachesTheUserOnFirstReadSoALaterNullHttpContextStaysLoggedIn()
    {
        var userId = Guid.NewGuid();
        var accessor = ContextWith(Authenticated(userId));
        var sut = new HttpContextUserAccessor(accessor.Object, new AmbientUserContext());

        // First read, while the request context is present.
        Assert.True(sut.IsLoggedIn);
        Assert.Equal(userId, sut.User.Id);

        // The interactive circuit drops HttpContext on later renders — the cache must hold, which
        // is what stops components flipping to "logged out" mid-session.
        accessor.SetupGet(a => a.HttpContext).Returns((HttpContext?)null);
        Assert.True(sut.IsLoggedIn);
        Assert.Equal(userId, sut.User.Id);
    }

    [Fact]
    public void ReportsLoggedOutWhenThereIsNoContextAndNothingCached()
    {
        var sut = new HttpContextUserAccessor(ContextWith(null).Object, new AmbientUserContext());

        Assert.False(sut.IsLoggedIn);
        Assert.Throws<ScoreTracker.Domain.Exceptions.UserNotLoggedInException>(() => sut.User);
    }

    [Fact]
    public void SetScopedUserEstablishesTheUserWithNoRequestContext()
    {
        var userId = Guid.NewGuid();
        var sut = new HttpContextUserAccessor(ContextWith(null).Object, new AmbientUserContext());

        sut.SetScopedUser(new User(userId, "Tester", true, null,
            new Uri("https://piuimages.arroweclip.se/avatars/x.png"), null, false));

        Assert.True(sut.IsLoggedIn);
        Assert.Equal(userId, sut.User.Id);
    }
}
