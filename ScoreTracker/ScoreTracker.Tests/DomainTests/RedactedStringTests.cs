using System.Text.Json;
using ScoreTracker.SharedKernel.ValueTypes;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public sealed class RedactedStringTests
{
    private sealed record CredentialCarrier(string Username, RedactedString Password);

    [Fact]
    public void ToStringMasksTheValue()
    {
        RedactedString secret = "hunter2";

        Assert.Equal("***", secret.ToString());
        Assert.Equal("***", $"{secret}");
    }

    [Fact]
    public void RecordToStringMasksTheValue()
    {
        var carrier = new CredentialCarrier("someone", "hunter2");

        Assert.DoesNotContain("hunter2", carrier.ToString());
        Assert.Contains("***", carrier.ToString());
    }

    [Fact]
    public void ImplicitStringConversionRevealsTheValue()
    {
        RedactedString secret = "hunter2";
        string revealed = secret;

        Assert.Equal("hunter2", revealed);
        Assert.Equal("hunter2", secret.Reveal());
    }

    [Fact]
    public void JsonSerializationRoundTripsTheRealValue()
    {
        var carrier = new CredentialCarrier("someone", "hunter2");

        var json = JsonSerializer.Serialize(carrier);
        var restored = JsonSerializer.Deserialize<CredentialCarrier>(json);

        Assert.Equal("hunter2", restored!.Password.Reveal());
    }

    [Fact]
    public void NullAndDefaultRevealAsEmpty()
    {
        Assert.Equal(string.Empty, RedactedString.From(null!).Reveal());
        Assert.Equal(string.Empty, default(RedactedString).Reveal());
    }
}
