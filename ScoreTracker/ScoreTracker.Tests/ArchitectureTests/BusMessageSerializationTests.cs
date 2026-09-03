using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using MassTransit;
using Xunit;

namespace ScoreTracker.Tests.ArchitectureTests;

/// <summary>
///     Bus messages survive a JSON round trip. The in-memory transport is still a transport: every
///     published message is serialized by MassTransit's own System.Text.Json options and rebuilt
///     before a consumer sees it, and those options know nothing about our value types.
///     <para>
///         A value type that hides its payload in a private field and exposes no public member
///         serializes as <c>{}</c> — no exception, no warning — and comes back as
///         <c>default</c>. That is how the Daily Step Discord card came to print every finished
///         placement as a 0-point F: <c>DailyStepResult.Score</c> was a <see cref="SharedKernel.ValueTypes.PhoenixScore" />,
///         which wrote <c>{}</c> and read back 0, while place, player and plate all arrived intact
///         so the card looked structurally fine. <c>CommunityDeletedEvent.CommunityName</c> lost
///         its <see cref="SharedKernel.ValueTypes.Name" /> the same way.
///     </para>
///     The fix is the one <see cref="SharedKernel.ValueTypes.RedactedString" /> already used: a
///     <see cref="JsonConverterAttribute" /> on the type itself, so the converter travels with the
///     type instead of waiting to be registered in someone's options bag. This ratchet holds that
///     line for every message the bus carries.
/// </summary>
public sealed class BusMessageSerializationTests
{
    private static readonly Assembly[] ConsumerAssemblies =
    {
        typeof(Application.Commands.SaveChartToListCommand).Assembly,
        typeof(PlayerProgress.Contracts.Queries.GetTop50CompetitiveQuery).Assembly,
        typeof(Domain.Models.User).Assembly,
        typeof(ScoreLedger.Contracts.Queries.GetPhoenixRecordQuery).Assembly,
        typeof(OfficialMirror.Contracts.Queries.GetGameCardsQuery).Assembly,
        typeof(Catalog.Contracts.Queries.GetChartsQuery).Assembly,
        typeof(Randomizer.Contracts.Queries.GetRandomSettingsQuery).Assembly,
        typeof(ChartIntelligence.Contracts.Messages.ProcessPassTierListCommand).Assembly,
        typeof(WeeklyChallenge.Contracts.Messages.RotateWeeklyChartsCommand).Assembly,
        typeof(EventCompetition.Contracts.Messages.TryScheduleMoMCommand).Assembly,
        typeof(Communities.Contracts.Commands.CreateCommunityCommand).Assembly,
        typeof(Identity.Contracts.Commands.CreateUserCommand).Assembly,
        typeof(Translations.Contracts.Messages.QueueTextForTranslationCommand).Assembly,
        typeof(CommunityTools.Contracts.Queries.GetMyToolsQuery).Assembly,
        typeof(ChartComments.Contracts.Queries.GetChartCommentsQuery).Assembly,
        typeof(HomePage.Contracts.Queries.GetMyHomePagesQuery).Assembly,
        typeof(Rivals.Contracts.Queries.GetMyRivalsQuery).Assembly
    };

    // The scalars System.Text.Json already knows how to write. The walk stops here rather than
    // inspecting their internals.
    private static readonly HashSet<Type> Scalars = new()
    {
        typeof(string), typeof(Guid), typeof(decimal), typeof(DateTime), typeof(DateTimeOffset),
        typeof(DateOnly), typeof(TimeOnly), typeof(TimeSpan), typeof(Uri), typeof(object),
        typeof(JsonElement), typeof(JsonDocument)
    };

    private static Type[] BusMessageTypes()
    {
        return ConsumerAssemblies.SelectMany(a => a.GetTypes())
            .SelectMany(t => t.GetInterfaces())
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IConsumer<>))
            .Select(i => i.GetGenericArguments()[0])
            .Distinct()
            .OrderBy(t => t.FullName, StringComparer.Ordinal)
            .ToArray();
    }

    [Fact]
    public void EveryBusMessageIsDiscoverable()
    {
        // A ratchet that walks nothing passes forever. If the vertical list above goes stale this
        // is what says so.
        Assert.True(BusMessageTypes().Length > 50);
    }

    [Fact]
    public void NoBusMessageCarriesAValueTypeThatSerializesAsAnEmptyObject()
    {
        var violations = new List<string>();
        foreach (var message in BusMessageTypes())
            Walk(message, message.Name, new HashSet<Type>(), violations);

        Assert.True(violations.Count == 0,
            "These bus-message members serialize as {} and arrive as default. Put a "
            + "[JsonConverter] on the type (see RedactedString) or carry a primitive on the "
            + $"contract:{Environment.NewLine}  {string.Join(Environment.NewLine + "  ", violations)}");
    }

    private static void Walk(Type type, string path, HashSet<Type> seen, List<string> violations)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (!seen.Add(type)) return;
        if (type.IsPrimitive || type.IsEnum || Scalars.Contains(type)) return;

        if (typeof(IEnumerable).IsAssignableFrom(type))
        {
            var elements = type.IsArray
                ? new[] { type.GetElementType()! }
                : type.GetGenericArguments();
            foreach (var element in elements) Walk(element, path, new HashSet<Type>(seen), violations);
            return;
        }

        // A tuple is a BCL type holding OUR types, so it has to be opened rather than skipped —
        // otherwise (int Place, PhoenixScore Score) walks straight past this ratchet.
        if (type.IsGenericType && type.FullName?.StartsWith("System.ValueTuple`", StringComparison.Ordinal) == true
            || type.IsGenericType && type.FullName?.StartsWith("System.Tuple`", StringComparison.Ordinal) == true)
        {
            foreach (var element in type.GetGenericArguments())
                Walk(element, path, new HashSet<Type>(seen), violations);
            return;
        }

        // Otherwise only our own types are judged: a BCL shape we don't control is not something
        // a contract author can fix, and guessing at one is how a ratchet earns a false positive.
        if (type.Assembly.GetName().Name?.StartsWith("ScoreTracker", StringComparison.Ordinal) != true) return;

        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead).ToArray();
        var publicFields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
        if (properties.Length == 0 && publicFields.Length == 0)
        {
            // No public member is only a problem when the type is HIDING something. An empty
            // trigger command carries no state, so {} is the honest wire form for it.
            var holdsState = type.GetFields(BindingFlags.NonPublic | BindingFlags.Instance).Length > 0;
            if (holdsState && type.GetCustomAttribute<JsonConverterAttribute>() == null)
                violations.Add($"{path} ({type.Name})");
            return;
        }

        foreach (var property in properties)
            Walk(property.PropertyType, $"{path}.{property.Name}", new HashSet<Type>(seen), violations);
        foreach (var field in publicFields)
            Walk(field.FieldType, $"{path}.{field.Name}", new HashSet<Type>(seen), violations);
    }
}
