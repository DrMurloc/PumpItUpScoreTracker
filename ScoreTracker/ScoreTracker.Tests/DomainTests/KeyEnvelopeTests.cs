using System;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using ScoreTracker.Data.Clients;
using ScoreTracker.Data.Configuration;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public sealed class KeyEnvelopeTests
{
    private static KeyEnvelope LocalEnvelope()
    {
        return new KeyEnvelope(Options.Create(new KeyVaultConfiguration
        {
            LocalKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
        }));
    }

    [Fact]
    public async Task WrapThenUnwrapReturnsTheOriginalDataKey()
    {
        var envelope = LocalEnvelope();
        var dataKey = RandomNumberGenerator.GetBytes(32);

        var wrapped = await envelope.Wrap(dataKey);
        var unwrapped = await envelope.Unwrap(wrapped);

        Assert.Equal(dataKey, unwrapped);
        Assert.NotEqual(dataKey, wrapped);
    }

    [Fact]
    public async Task TamperedWrappedKeyFailsToUnwrap()
    {
        var envelope = LocalEnvelope();
        var wrapped = await envelope.Wrap(RandomNumberGenerator.GetBytes(32));
        wrapped[^1] ^= 0xFF;

        await Assert.ThrowsAsync<AuthenticationTagMismatchException>(() => envelope.Unwrap(wrapped));
    }

    [Fact]
    public async Task WrappingTheSameKeyTwiceProducesDifferentOutput()
    {
        var envelope = LocalEnvelope();
        var dataKey = RandomNumberGenerator.GetBytes(32);

        var first = await envelope.Wrap(dataKey);
        var second = await envelope.Wrap(dataKey);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public async Task MissingKeyConfigurationThrowsAClearError()
    {
        var envelope = new KeyEnvelope(Options.Create(new KeyVaultConfiguration()));

        await Assert.ThrowsAsync<InvalidOperationException>(() => envelope.Wrap(RandomNumberGenerator.GetBytes(32)));
    }
}
