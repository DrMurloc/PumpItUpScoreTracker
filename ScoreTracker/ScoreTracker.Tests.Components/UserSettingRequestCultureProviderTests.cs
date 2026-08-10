using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ScoreTracker.Identity.Contracts.Queries;
using ScoreTracker.Web.Services.Localization;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     Rank 2 of the resolution order: a signed-in player's saved language beats the cookie and
///     the browser. Declining (returning null) is just as load-bearing as answering — it is what
///     leaves the rest of the chain intact for everyone else.
/// </summary>
public sealed class UserSettingRequestCultureProviderTests
{
    private readonly Mock<IMediator> _mediator = new();

    private HttpContext Context(ClaimsPrincipal? user, IDictionary<string, string>? settings = null)
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetUserUiSettingsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings ?? new Dictionary<string, string>());

        var services = new ServiceCollection();
        services.AddMemoryCache();
        services.AddLogging();
        services.AddSingleton(_mediator.Object);

        return new DefaultHttpContext
        {
            User = user ?? new ClaimsPrincipal(new ClaimsIdentity()),
            RequestServices = services.BuildServiceProvider()
        };
    }

    // A non-null authentication type is what makes Identity.IsAuthenticated true.
    private static ClaimsPrincipal SignedIn(Guid userId)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) }, "TestAuth"));
    }

    [Fact]
    public async Task AnswersWithTheLanguageTheAccountSaved()
    {
        var context = Context(SignedIn(Guid.NewGuid()),
            new Dictionary<string, string> { ["Culture"] = "en-US" });

        var result = await new UserSettingRequestCultureProvider().DetermineProviderCultureResult(context);

        Assert.Equal("en-US", Assert.Single(result!.UICultures).Value);
    }

    /// <summary>
    ///     The reported bug in one assertion: the account says English, so it renders English —
    ///     a Spanish browser and a missing cookie no longer get a vote.
    /// </summary>
    [Fact]
    public async Task OutranksTheBrowserForASignedInPlayer()
    {
        var context = Context(SignedIn(Guid.NewGuid()),
            new Dictionary<string, string> { ["Culture"] = "en-US" });
        context.Request.Headers.AcceptLanguage = "es-ES,es;q=0.9";

        var result = await new UserSettingRequestCultureProvider().DetermineProviderCultureResult(context);

        Assert.Equal("en-US", Assert.Single(result!.Cultures).Value);
    }

    [Fact]
    public async Task DeclinesForAnAnonymousVisitor()
    {
        var context = Context(null, new Dictionary<string, string> { ["Culture"] = "en-US" });

        Assert.Null(await new UserSettingRequestCultureProvider().DetermineProviderCultureResult(context));
        _mediator.Verify(m => m.Send(It.IsAny<GetUserUiSettingsQuery>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DeclinesWhenThePlayerHasNeverChosenALanguage()
    {
        var context = Context(SignedIn(Guid.NewGuid()),
            new Dictionary<string, string> { ["Universal__CurrentMix"] = "Phoenix2" });

        Assert.Null(await new UserSettingRequestCultureProvider().DetermineProviderCultureResult(context));
    }

    /// <summary>A retired or mistyped code is not a language — decline rather than force English.</summary>
    [Fact]
    public async Task DeclinesOnAnUnsupportedStoredCode()
    {
        var context = Context(SignedIn(Guid.NewGuid()),
            new Dictionary<string, string> { ["Culture"] = "zz-ZZ" });

        Assert.Null(await new UserSettingRequestCultureProvider().DetermineProviderCultureResult(context));
    }

    [Fact]
    public async Task NormalizesStoredCasingToTheCanonicalCode()
    {
        var context = Context(SignedIn(Guid.NewGuid()),
            new Dictionary<string, string> { ["Culture"] = "ES-es" });

        var result = await new UserSettingRequestCultureProvider().DetermineProviderCultureResult(context);

        Assert.Equal("es-ES", Assert.Single(result!.UICultures).Value);
    }

    /// <summary>
    ///     This runs on every request, static assets included, so it has to be a cache read. The
    ///     entry is the shell's own — evicted together whenever a setting is saved.
    /// </summary>
    [Fact]
    public async Task ReadsTheSettingsOnceAndServesTheCacheAfterwards()
    {
        var context = Context(SignedIn(Guid.NewGuid()),
            new Dictionary<string, string> { ["Culture"] = "ja-JP" });
        var provider = new UserSettingRequestCultureProvider();

        await provider.DetermineProviderCultureResult(context);
        await provider.DetermineProviderCultureResult(context);

        _mediator.Verify(m => m.Send(It.IsAny<GetUserUiSettingsQuery>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    ///     A language lookup is never worth an error page — it declines and the cookie and the
    ///     browser answer instead.
    /// </summary>
    [Fact]
    public async Task DeclinesRatherThanThrowingWhenTheSettingsCannotBeRead()
    {
        var context = Context(SignedIn(Guid.NewGuid()));
        _mediator.Setup(m => m.Send(It.IsAny<GetUserUiSettingsQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("database is having a day"));

        Assert.Null(await new UserSettingRequestCultureProvider().DetermineProviderCultureResult(context));
    }
}
