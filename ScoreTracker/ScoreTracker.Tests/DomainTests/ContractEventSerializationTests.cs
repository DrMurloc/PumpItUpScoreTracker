using System;
using System.Text.Json;
using ScoreTracker.Domain.Events;
using ScoreTracker.CommunityTools.Contracts;
using ScoreTracker.SharedKernel.Enums;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

/// <summary>
///     Contract events double as partner webhook bodies (ADR-001 D3): they must
///     round-trip plain JSON with no custom converters registered.
///     Commit 5 (Phoenix 2 plan) adds an additive Mix field to the score contract
///     events — deliberately pinned here with a non-default value; SchemaVersion
///     stays 1 because the change is additive (missing mix on old payloads = Phoenix).
/// </summary>
public sealed class ContractEventSerializationTests
{
    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web);

    private static void AssertRoundTrips<T>(T contractEvent)
    {
        var json = JsonSerializer.Serialize(contractEvent, Wire);
        var rehydrated = JsonSerializer.Deserialize<T>(json, Wire);
        Assert.Equal(json, JsonSerializer.Serialize(rehydrated, Wire));
    }

    [Fact]
    public void PlayerScoresUpdatedEventRoundTripsJson()
    {
        AssertRoundTrips(PlayerScoresUpdatedEvent.Create(
            new DateTimeOffset(2026, 6, 12, 0, 0, 0, TimeSpan.Zero),
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            MixEnum.Phoenix2,
            new[]
            {
                new PlayerScoresUpdatedEvent.ScoreChange(
                    Guid.Parse("11111111-1111-1111-1111-111111111111"),
                    IsNewPass: true, OldScore: null, NewScore: 985000, Plate: "ExtremeGame", IsBroken: false),
                new PlayerScoresUpdatedEvent.ScoreChange(
                    Guid.Parse("22222222-2222-2222-2222-222222222222"),
                    IsNewPass: false, OldScore: 900000, NewScore: 950000, Plate: null, IsBroken: false)
            }));
    }

    [Fact]
    public void ScoreImportCompletedEventRoundTripsJson()
    {
        AssertRoundTrips(ScoreImportCompletedEvent.Create(
            new DateTimeOffset(2026, 6, 12, 0, 0, 0, TimeSpan.Zero),
            ScoreImportCompletedEvent.OfficialImportSource,
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            MixEnum.Phoenix2,
            new[]
            {
                new ScoreImportCompletedEvent.ImportedScore(
                    Guid.Parse("11111111-1111-1111-1111-111111111111"), 985000, "ExtremeGame", false)
            }));
    }

    [Fact]
    public void ContractEventsCarryTheEnvelope()
    {
        var e = PlayerScoresUpdatedEvent.Create(
            new DateTimeOffset(2026, 6, 12, 0, 0, 0, TimeSpan.Zero), Guid.NewGuid(), MixEnum.Phoenix2,
            Array.Empty<PlayerScoresUpdatedEvent.ScoreChange>());

        Assert.NotEqual(Guid.Empty, e.EventId);
        Assert.Equal(PlayerScoresUpdatedEvent.CurrentSchemaVersion, e.SchemaVersion);
        Assert.Equal(new DateTimeOffset(2026, 6, 12, 0, 0, 0, TimeSpan.Zero), e.OccurredAt);
        Assert.Equal(MixEnum.Phoenix2, e.Mix);
    }

    [Fact]
    public void ScoreContractEventsCarryTheMixOnTheWire()
    {
        // Additive Phoenix 2 field: the wire payload must actually contain the mix
        // (not silently drop it), so partners can split parallel-mix traffic.
        var json = JsonSerializer.Serialize(PlayerScoresUpdatedEvent.Create(
            new DateTimeOffset(2026, 6, 12, 0, 0, 0, TimeSpan.Zero), Guid.NewGuid(), MixEnum.Phoenix2,
            Array.Empty<PlayerScoresUpdatedEvent.ScoreChange>()), Wire);

        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.TryGetProperty("mix", out var mix));
        Assert.Equal((int)MixEnum.Phoenix2, mix.GetInt32());
    }

    /// <summary>
    ///     The webhook envelope is a published contract, not an internal DTO — a maker's parser
    ///     breaks when it changes. Round-tripping is the cheap half of pinning it: a field that
    ///     serializes but does not deserialize is a shape nobody can consume.
    /// </summary>
    [Fact]
    public void TheDeliveryEnvelopeRoundTripsJson()
    {
        AssertRoundTrips(new DeliveryPayload(
            "d-4f819c", DeliveryPayload.CurrentSchemaVersion,
            new DateTimeOffset(2026, 8, 2, 14, 21, 55, TimeSpan.Zero), false,
            new DeliveryPayload.PlayerBlock("Phoenix", "phoenix",
                Guid.Parse("44444444-4444-4444-4444-444444444444"), "DrMurloc", "MURLOC#1"),
            null,
            new[]
            {
                new DeliveryPayload.Change(Guid.Parse("55555555-5555-5555-5555-555555555555"),
                    true, 990_000, 999_231, null, null, "PerfectGame", false)
            },
            null));
    }

    /// <summary>
    ///     The legacy half of the same envelope: letter grades set, scores null. A tool branching on
    ///     scoringModel has to survive both, and nulls are the whole point of the discriminator.
    /// </summary>
    [Fact]
    public void ALegacyChangeRoundTripsWithItsNulls()
    {
        AssertRoundTrips(new DeliveryPayload.Change(
            Guid.Parse("55555555-5555-5555-5555-555555555555"),
            false, null, null, "A", "AA", null, true));
    }
}
