using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Web.Controllers.Api.V2;

namespace ScoreTracker.Tests.Api;

/// <summary>
///     The conventions every api/v2 endpoint inherits. These are wire contract even though the types
///     are internal: a caller depends on the mix vocabulary, on cursors being opaque-but-durable, and
///     on a stale cursor failing loudly instead of returning shifted rows.
/// </summary>
public sealed class V2ConventionTests
{
    [Theory]
    [InlineData("Phoenix", MixEnum.Phoenix)]
    [InlineData("phoenix2", MixEnum.Phoenix2)]
    [InlineData("XX", MixEnum.XX)]
    [InlineData("  FiestaEx  ", MixEnum.FiestaEx)]
    [InlineData("prime2", MixEnum.Prime2)]
    [InlineData("FirstDanceFloor", MixEnum.FirstDanceFloor)]
    public void EveryMixIsReachableByEnumName(string raw, MixEnum expected)
    {
        Assert.True(V2MixParser.TryParse(raw, out var mix));
        Assert.Equal(expected, mix);
    }

    [Fact]
    public void AllThirtyMixesParse()
    {
        foreach (var mix in Enum.GetValues<MixEnum>())
        {
            Assert.True(V2MixParser.TryParse(mix.ToString(), out var parsed), $"{mix} did not parse");
            Assert.Equal(mix, parsed);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MixIsRequired(string? raw)
    {
        Assert.False(V2MixParser.TryParse(raw, out _));
    }

    // A number would bind to whatever member currently sits at that position, and the enum is
    // append-only precisely because those positions move.
    [Theory]
    [InlineData("7")]
    [InlineData("0")]
    [InlineData("Phoenix 2")]
    [InlineData("NotAMix")]
    public void NumericAndDisplayFormsAreRejected(string raw)
    {
        Assert.False(V2MixParser.TryParse(raw, out _));
    }

    [Fact]
    public void OffsetCursorRoundTrips()
    {
        var fingerprint = ContinuationToken.FingerprintOf("Phoenix", 20);
        var token = ContinuationToken.FromOffset(200, fingerprint);

        Assert.True(ContinuationToken.TryDecode(token.Encode(), fingerprint, out var decoded));
        Assert.Equal(200, decoded.Offset);
        Assert.Null(decoded.Key);
    }

    [Fact]
    public void KeysetCursorRoundTripsWithItsTiebreaker()
    {
        var id = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var fingerprint = ContinuationToken.FingerprintOf("Phoenix");
        var token = ContinuationToken.FromKeyset("2026-07-30T22:08:24.0000000+00:00", id, fingerprint);

        Assert.True(ContinuationToken.TryDecode(token.Encode(), fingerprint, out var decoded));
        Assert.Equal("2026-07-30T22:08:24.0000000+00:00", decoded.Key);
        Assert.Equal(id, decoded.Id);
    }

    // The whole point of the fingerprint: replaying a cursor against different filters would
    // otherwise return quietly wrong rows.
    [Fact]
    public void CursorFromDifferentFiltersIsRejected()
    {
        var token = ContinuationToken.FromOffset(200, ContinuationToken.FingerprintOf("Phoenix", 20));

        Assert.False(ContinuationToken.TryDecode(token.Encode(),
            ContinuationToken.FingerprintOf("Phoenix", 21), out _));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-base64!!")]
    [InlineData("YWJj")]
    public void MalformedCursorsAreRejectedRatherThanThrowing(string? raw)
    {
        Assert.False(ContinuationToken.TryDecode(raw, 0, out _));
    }

    [Fact]
    public void CursorSurvivesAQueryStringWithoutEscaping()
    {
        var encoded = ContinuationToken
            .FromKeyset("2026-07-30T22:08:24.0000000+00:00", Guid.NewGuid(), 12345)
            .Encode();

        Assert.Equal(encoded, Uri.EscapeDataString(encoded));
    }

    /// <summary>
    ///     Every v2 action tells Swagger what a 200 looks like. Without the declaration the docs
    ///     page shows a bare "200 Success" with no schema — which is what the whole v2 surface
    ///     showed until 2026-09-05 — and a maker learns the shape by calling. Additive: a new
    ///     action fails here until it declares its type.
    /// </summary>
    [Fact]
    public void EveryV2ActionDeclaresItsSuccessShape()
    {
        var offenders = typeof(ApiV2ControllerBase).Assembly.GetTypes()
            .Where(t => !t.IsAbstract && typeof(ApiV2ControllerBase).IsAssignableFrom(t))
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => m.GetCustomAttributes<HttpMethodAttribute>().Any()))
            .Where(m => !m.GetCustomAttributes<ProducesResponseTypeAttribute>()
                .Any(a => a.StatusCode == StatusCodes.Status200OK && a.Type != typeof(void)))
            .Select(m => $"{m.DeclaringType!.Name}.{m.Name}")
            .OrderBy(x => x)
            .ToArray();

        Assert.Empty(offenders);
    }
}
