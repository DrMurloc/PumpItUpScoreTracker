using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using ScoreTracker.CommunityTools.Contracts;
using ScoreTracker.CommunityTools.Contracts.Queries;
using ScoreTracker.Domain.Models;
using ScoreTracker.Identity.Contracts.Queries;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Security;

namespace ScoreTracker.Tests.Api;

/// <summary>
///     How a caller proves who they are on api/v2. Two credentials, three ways of presenting them,
///     and the rule that a tool never resolves to a user.
///     <para>
///         Field-tested into existence: a freshly minted tool key, put where v1 taught everyone to
///         put a credential, came back 401 with nothing on screen explaining it.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class V2AuthenticationTests
{
    private const string ToolKey = "piu_scores_live_" + Sixty4;
    private const string LegacyToolKey = "pst_live_" + Sixty4;
    private const string Sixty4 = "4f8c21ab90de7715c3a06b28f4e15d934f8c21ab90de7715c3a06b28f4e15d93";

    private static readonly Guid AToolId = Guid.Parse("aaaaaaaa-0000-0000-0000-00000000000a");
    private static readonly Guid AUserId = Guid.Parse("bbbbbbbb-0000-0000-0000-00000000000b");
    private static readonly Guid APersonalToken = Guid.Parse("cccccccc-0000-0000-0000-00000000000c");

    private static Mock<IMediator> Mediator()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.Is<GetToolByApiKeyQuery>(q => q.Key == ToolKey || q.Key == LegacyToolKey),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolKeyPrincipal(AToolId, "production"));
        mediator.Setup(m => m.Send(It.Is<GetToolByApiKeyQuery>(q => q.Key != ToolKey && q.Key != LegacyToolKey),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((ToolKeyPrincipal?)null);
        mediator.Setup(m => m.Send(It.Is<GetUserByApiTokenQuery>(q => q.ApiToken == APersonalToken),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User(AUserId, Name.From("DrMurloc"), true, null,
                new Uri("https://piuscores.arroweclip.se/avatar.png"), null));
        mediator.Setup(m => m.Send(It.Is<GetUserByApiTokenQuery>(q => q.ApiToken != APersonalToken),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        return mediator;
    }

    private static async Task<AuthenticateResult> Authenticate(string? header)
    {
        var services = new ServiceCollection();
        services.AddSingleton(Mediator().Object);

        var context = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        if (header is not null) context.Request.Headers["Authorization"] = header;

        var accessor = new Mock<IHttpContextAccessor>();
        accessor.Setup(a => a.HttpContext).Returns(context);

        var options = new Mock<IOptionsMonitor<AuthenticationSchemeOptions>>();
        options.Setup(o => o.Get(It.IsAny<string>())).Returns(new AuthenticationSchemeOptions());

        var handler = new ToolKeyAuthenticationScheme(options.Object, NullLoggerFactory.Instance,
            UrlEncoder.Default, new FrozenClock(), accessor.Object);
        await handler.InitializeAsync(
            new AuthenticationScheme(ToolKeyAuthenticationScheme.SchemeName, null,
                typeof(ToolKeyAuthenticationScheme)), context);

        return await handler.AuthenticateAsync();
    }

    private static string Basic(string user, string password)
    {
        return "Basic " + Convert.ToBase64String(
            Encoding.GetEncoding("iso-8859-1").GetBytes($"{user}:{password}"));
    }

    [Fact]
    public async Task ABearerToolKeyResolvesToTheTool()
    {
        var result = await Authenticate($"Bearer {ToolKey}");

        Assert.True(result.Succeeded);
        Assert.Equal(AToolId.ToString(),
            result.Principal!.FindFirstValue(ToolKeyAuthenticationScheme.ToolIdClaim));
        Assert.Equal("production", result.Principal.FindFirstValue(ToolKeyAuthenticationScheme.KeyNameClaim));
    }

    /// <summary>
    ///     The reason this file exists. v1 documented "personal token in the password box, anything
    ///     for the username", so that is the first thing a maker tries with their new tool key —
    ///     and rejecting it taught them only that the key was broken.
    /// </summary>
    [Fact]
    public async Task AToolKeyInTheBasicPasswordPositionAlsoResolvesToTheTool()
    {
        var result = await Authenticate(Basic("anything", ToolKey));

        Assert.True(result.Succeeded);
        Assert.Equal(AToolId.ToString(),
            result.Principal!.FindFirstValue(ToolKeyAuthenticationScheme.ToolIdClaim));
        Assert.Equal("production", result.Principal.FindFirstValue(ToolKeyAuthenticationScheme.KeyNameClaim));
    }

    /// <summary>
    ///     A key minted before the prefix changed is still a key: the stored hash covers the whole
    ///     string, so rotating what we mint must not 401 anyone holding an older one.
    /// </summary>
    [Fact]
    public async Task AKeyOnTheOlderPrefixStillAuthenticates()
    {
        Assert.True((await Authenticate($"Bearer {LegacyToolKey}")).Succeeded);
        Assert.True((await Authenticate(Basic("x", LegacyToolKey))).Succeeded);
    }

    [Fact]
    public async Task APersonalTokenResolvesToItsUserAndNeverToATool()
    {
        var result = await Authenticate(Basic("anything", APersonalToken.ToString()));

        Assert.True(result.Succeeded);
        Assert.Null(result.Principal!.FindFirstValue(ToolKeyAuthenticationScheme.ToolIdClaim));
        Assert.Null(result.Principal.FindFirstValue(ToolKeyAuthenticationScheme.KeyNameClaim));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Bearer not-a-key")]
    [InlineData("Digest whatever")]
    public async Task NothingElseAuthenticates(string? header)
    {
        Assert.False((await Authenticate(header)).Succeeded);
    }

    [Fact]
    public async Task AnUnknownPasswordIsRejectedRatherThanTreatedAsEither()
    {
        Assert.False((await Authenticate(Basic("user", "hunter2"))).Succeeded);
        Assert.False((await Authenticate(Basic("user", Guid.NewGuid().ToString()))).Succeeded);
    }

    /// <summary>ISystemClock is obsolete but still the handler's constructor parameter.</summary>
#pragma warning disable CS0618
    private sealed class FrozenClock : ISystemClock
    {
        public DateTimeOffset UtcNow => new(2026, 8, 3, 0, 0, 0, TimeSpan.Zero);
    }
#pragma warning restore CS0618
}
