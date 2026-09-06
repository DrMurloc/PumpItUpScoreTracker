using System.Diagnostics.CodeAnalysis;
using System.Text;
using ScoreTracker.Web.Security;

namespace ScoreTracker.Tests.Api;

/// <summary>
///     What the v2 limiter counts a caller by. The ceiling itself is a constant; the partition is
///     the part that can quietly hand one caller several ceilings.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class ApiV2RateLimitingTests
{
    private const string Key = "piu_scores_live_4f8c21ab90de7715c3a06b28f4e15d934f8c21ab90de7715c3a06b28f4e15d93";

    private static string Basic(string user, string password)
    {
        return "Basic " + Convert.ToBase64String(
            Encoding.GetEncoding("iso-8859-1").GetBytes($"{user}:{password}"));
    }

    private static string Partition(string? header)
    {
        return ApiV2RateLimiting.PartitionKey(ApiCredential.Parse(header));
    }

    /// <summary>
    ///     Bearer, Bearer with stray whitespace, and the Basic password box are three spellings of
    ///     one credential. The raw header gave them three buckets of 600 a minute each.
    /// </summary>
    [Fact]
    public void OneKeyIsOneBucketHoweverItIsPresented()
    {
        var bearer = Partition($"Bearer {Key}");

        Assert.Equal(bearer, Partition($"Bearer   {Key}  "));
        Assert.Equal(bearer, Partition(Basic("anything", Key)));
        Assert.NotEqual(bearer, Partition("Bearer piu_scores_live_" + new string('0', 64)));
    }

    [Fact]
    public void ThePartitionNeverHoldsTheSecretItself()
    {
        var partition = Partition($"Bearer {Key}");

        Assert.DoesNotContain(Key, partition);
        Assert.DoesNotContain(Key[^12..], partition);
    }

    [Fact]
    public void APersonalTokenIsItsOwnBucket()
    {
        var token = Guid.NewGuid().ToString();

        Assert.Equal(Partition(Basic("x", token)), Partition(Basic("y", token)));
        Assert.NotEqual(Partition(Basic("x", token)), Partition(Basic("x", Guid.NewGuid().ToString())));
    }

    /// <summary>Nothing to partition on shares one bucket: it is about to be rejected anyway.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Bearer ")]
    [InlineData("Digest whatever")]
    public void AnUnreadableOrEmptyCredentialSharesTheAnonymousBucket(string? header)
    {
        Assert.Equal("anonymous", Partition(header));
    }
}
