using System;
using System.Linq;
using System.Reflection;
using ScoreTracker.CommunityTools.Contracts;
using ScoreTracker.CommunityTools.Domain;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.ValueTypes;
using Xunit;

namespace ScoreTracker.Tests.ArchitectureTests;

/// <summary>
///     A PIUGame session delivery carries a live credential to piugame.com. Every other webhook mode
///     keeps its body for seven days so a maker can replay it; this one must keep nothing.
///     <para>
///         The behavioural half of the rule is covered by <c>WebhookDeliveryTests</c> — for every
///         status, <c>WebhookRetention.ShouldPersistBody</c> says no for session mode. What that
///         cannot catch is a delivery path that never asks: a new caller wiring the session client
///         to the delivery repository directly, or a column appearing on an entity that would hold
///         the sid outright. Those are structural, so they are ratcheted structurally.
///     </para>
/// </summary>
public sealed class SessionModeNeverPersistsBodyTests
{
    private static readonly Assembly CommunityTools = typeof(DeliveryPayload).Assembly;

    /// <summary>
    ///     Anything that stores a delivery. A session deliverer must be unable to reach one, which is
    ///     a stronger and more durable guarantee than trusting it not to call one.
    /// </summary>
    private static bool IsDeliveryPersistence(Type type)
    {
        return type == typeof(IWebhookDeliveryRepository)
               || type.Name.EndsWith("DeliveryRepository", StringComparison.Ordinal)
               || type.Name.EndsWith("DeliveryDispatcher", StringComparison.Ordinal);
    }

    [Fact]
    public void SessionDeliveryImplementationsCannotReachDeliveryPersistence()
    {
        var implementations = CommunityTools.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false })
            .Where(t => typeof(ISessionDeliveryClient).IsAssignableFrom(t))
            .ToArray();

        Assert.True(implementations.Length > 0,
            $"{nameof(ISessionDeliveryClient)} has no implementation in {CommunityTools.GetName().Name} — " +
            "this ratchet would pass vacuously.");

        var violations = implementations
            .SelectMany(t => t.GetConstructors()
                .SelectMany(c => c.GetParameters())
                .Select(p => p.ParameterType)
                .Concat(t.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                    .Select(f => f.FieldType))
                .Where(IsDeliveryPersistence)
                .Select(dep => $"{t.Name} depends on {dep.Name}"))
            .Distinct()
            .ToArray();

        Assert.True(violations.Length == 0,
            "A session delivery carries a live piugame.com credential and must never be written down. " +
            $"Remove the dependency, do not add a conditional: {string.Join("; ", violations)}");
    }

    /// <summary>
    ///     The other way the sid could land in the database: a column for it. RedactedString masks
    ///     <c>ToString()</c> but its JSON converter round-trips the real value, so a property typed
    ///     that way looks guarded and persists in plaintext.
    /// </summary>
    [Fact]
    public void NoCommunityToolsEntityCanHoldASession()
    {
        var violations = CommunityTools.GetTypes()
            .Where(t => t.Namespace?.EndsWith(".Infrastructure.Entities", StringComparison.Ordinal) == true)
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.PropertyType == typeof(RedactedString)
                            || p.Name.Equals("Sid", StringComparison.OrdinalIgnoreCase)
                            || p.Name.Contains("SessionKey", StringComparison.OrdinalIgnoreCase)
                            || p.Name.Contains("PiuGameSession", StringComparison.OrdinalIgnoreCase))
                .Select(p => $"{t.Name}.{p.Name}"))
            .ToArray();

        Assert.True(violations.Length == 0,
            $"A piugame session must not be stored: {string.Join(", ", violations)}");
    }
}
