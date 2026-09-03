using System;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using ScoreTracker.SharedKernel.ValueTypes;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public sealed class ValueTypeJsonConverterTests
{
    private static JsonSerializerOptions OptionsFor(System.Text.Json.Serialization.JsonConverter converter)
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(converter);
        return options;
    }

    // ---- Name ----

    [Fact]
    public void NameConverterRoundTripsAValidName()
    {
        var options = OptionsFor(Name.Converter);
        var original = Name.From("Hello World");

        var json = JsonSerializer.Serialize(original, options);
        var roundTripped = JsonSerializer.Deserialize<Name>(json, options);

        Assert.Equal("\"Hello World\"", json);
        Assert.Equal(original, roundTripped);
    }

    [Fact]
    public void NameConverterReadingEmptyStringThrows()
    {
        var options = OptionsFor(Name.Converter);
        Assert.Throws<ScoreTracker.Domain.Exceptions.InvalidNameException>(
            () => JsonSerializer.Deserialize<Name>("\"\"", options));
    }

    // ---- LevelBucket ----

    [Fact]
    public void LevelBucketConverterRoundTripsAValidBucket()
    {
        var options = OptionsFor(LevelBucket.Converter);
        var original = LevelBucket.From("S20");

        var json = JsonSerializer.Serialize(original, options);
        var roundTripped = JsonSerializer.Deserialize<LevelBucket>(json, options);

        Assert.Equal("\"S20\"", json);
        Assert.Equal(original.ToString(), roundTripped.ToString());
    }

    // ---- PhoenixScore ----

    [Fact]
    public void PhoenixScoreConverterRoundTripsScoreAsInteger()
    {
        var options = OptionsFor(PhoenixScore.Converter);
        var original = (PhoenixScore)950000;

        var json = JsonSerializer.Serialize(original, options);
        var roundTripped = JsonSerializer.Deserialize<PhoenixScore>(json, options);

        Assert.Equal("950000", json);
        Assert.Equal(original, roundTripped);
    }

    [Fact]
    public void PhoenixScoreConverterRejectsScoresAboveMax()
    {
        var options = OptionsFor(PhoenixScore.Converter);
        Assert.Throws<ScoreTracker.Domain.Exceptions.InvalidScoreException>(
            () => JsonSerializer.Deserialize<PhoenixScore>("1500000", options));
    }

    // ---- Rating ----

    [Fact]
    public void RatingConverterRoundTripsRatingAsInteger()
    {
        var options = OptionsFor(Rating.Converter);
        var original = (Rating)1234;

        var json = JsonSerializer.Serialize(original, options);
        var roundTripped = JsonSerializer.Deserialize<Rating>(json, options);

        Assert.Equal("1234", json);
        Assert.Equal(original, roundTripped);
    }

    [Fact]
    public void RatingConverterRejectsNegativeRatings()
    {
        var options = OptionsFor(Rating.Converter);
        Assert.Throws<ScoreTracker.Domain.Exceptions.InvalidScoreException>(
            () => JsonSerializer.Deserialize<Rating>("-1", options));
    }

    // ---- The converter travels with the type ----

    // Each of these wraps a private field and exposes no public member, so an options bag that
    // has NOT been handed the converter writes {} and reads back default -- silently. The
    // [JsonConverter] attribute on the type is what makes an un-configured serializer (MassTransit's,
    // for one) round-trip the value; ArchitectureTests/BusMessageSerializationTests holds the line
    // for the messages themselves.
    [Theory]
    [InlineData(typeof(Name))]
    [InlineData(typeof(PhoenixScore))]
    [InlineData(typeof(Rating))]
    [InlineData(typeof(LevelBucket))]
    [InlineData(typeof(RedactedString))]
    public void OpaqueValueTypesDeclareTheirOwnConverter(Type valueType)
    {
        Assert.NotNull(valueType.GetCustomAttribute<JsonConverterAttribute>());
    }

    [Fact]
    public void NameRoundTripsWithoutARegisteredConverter()
    {
        var json = JsonSerializer.Serialize(Name.From("Murloc Lab"));

        Assert.Equal("\"Murloc Lab\"", json);
        Assert.Equal("Murloc Lab", (string)JsonSerializer.Deserialize<Name>(json));
    }

    [Fact]
    public void PhoenixScoreRoundTripsWithoutARegisteredConverter()
    {
        var json = JsonSerializer.Serialize((PhoenixScore)987654);

        Assert.Equal("987654", json);
        Assert.Equal(987654, (int)JsonSerializer.Deserialize<PhoenixScore>(json));
    }

    [Fact]
    public void RatingRoundTripsWithoutARegisteredConverter()
    {
        var json = JsonSerializer.Serialize((Rating)1234);

        Assert.Equal("1234", json);
        Assert.Equal(1234, (int)JsonSerializer.Deserialize<Rating>(json));
    }

    [Fact]
    public void LevelBucketRoundTripsWithoutARegisteredConverter()
    {
        var json = JsonSerializer.Serialize(LevelBucket.From("S20"));

        Assert.Equal("\"S20\"", json);
        Assert.Equal("S20", JsonSerializer.Deserialize<LevelBucket>(json).ToString());
    }
}
