using System;
using System.Linq;
using System.Reflection;
using MediatR;
using ScoreTracker.SharedKernel.Messaging;
using Xunit;

namespace ScoreTracker.Tests.ArchitectureTests;

/// <summary>
///     Message taxonomy ratchets: queries vs commands vs events are distinguished by
///     folder, name, and interface (owner directive, 2026-06-12):
///     - Queries:  *Query records implementing IQuery&lt;T&gt;, in a Queries/ folder. Never on the bus.
///     - Commands: *Command records — MediatR ones implement IRequest and live in Commands/;
///       bus trigger messages are plain records in Application/Messages.
///     - Events:   *Event records (facts / notifications), never IRequest.
/// </summary>
public sealed class MessageTaxonomyTests
{
    private static readonly Assembly[] MessageAssemblies =
    {
        typeof(Application.Commands.CreateUserCommand).Assembly,
        typeof(PersonalProgress.Queries.GetTop50CompetitiveQuery).Assembly,
        typeof(Domain.Models.User).Assembly,
        typeof(Ucs.Contracts.UcsChart).Assembly,
        typeof(ScoreLedger.Contracts.Queries.GetPhoenixRecordQuery).Assembly,
        typeof(OfficialMirror.Contracts.Queries.GetGameCardsQuery).Assembly
    };

    private static Type[] TypesIn(string namespaceSuffix)
    {
        return MessageAssemblies.SelectMany(a => a.GetTypes())
            .Where(t => !t.IsNested && !t.Name.Contains('<') && !t.IsInterface
                        && t.Namespace != null && t.Namespace.EndsWith(namespaceSuffix, StringComparison.Ordinal))
            .ToArray();
    }

    [Fact]
    public void QueriesEndWithQueryAndImplementIQuery()
    {
        var violations = TypesIn(".Queries")
            .Where(t => !t.Name.EndsWith("Query", StringComparison.Ordinal)
                        || !t.GetInterfaces().Any(i =>
                            i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IQuery<>)))
            .Select(t => t.FullName)
            .ToArray();

        Assert.True(violations.Length == 0,
            $"Types in Queries folders must be *Query implementing IQuery<T>: {string.Join(", ", violations)}");
    }

    [Fact]
    public void MediatRCommandsEndWithCommandAndAreRequests()
    {
        var violations = TypesIn(".Commands")
            .Where(t => !t.Name.EndsWith("Command", StringComparison.Ordinal)
                        || !typeof(IBaseRequest).IsAssignableFrom(t))
            .Select(t => t.FullName)
            .ToArray();

        Assert.True(violations.Length == 0,
            $"Types in Commands folders must be *Command implementing IRequest: {string.Join(", ", violations)}");
    }

    [Fact]
    public void BusTriggerMessagesEndWithCommandAndAreNotRequests()
    {
        var violations = TypesIn(".Messages")
            .Where(t => !t.Name.EndsWith("Command", StringComparison.Ordinal)
                        || typeof(IBaseRequest).IsAssignableFrom(t))
            .Select(t => t.FullName)
            .ToArray();

        Assert.True(violations.Length == 0,
            $"Application/Messages types must be *Command and bus-only (not IRequest): {string.Join(", ", violations)}");
    }

    [Fact]
    public void EventsEndWithEventAndAreNotRequests()
    {
        var violations = TypesIn(".Events")
            .Where(t => !t.Name.EndsWith("Event", StringComparison.Ordinal)
                        || typeof(IBaseRequest).IsAssignableFrom(t))
            .Select(t => t.FullName)
            .ToArray();

        Assert.True(violations.Length == 0,
            $"Events folders must hold *Event facts/notifications, never IRequest: {string.Join(", ", violations)}");
    }

    [Fact]
    public void QueryNamedTypesLiveInQueriesFoldersOnly()
    {
        var violations = MessageAssemblies.SelectMany(a => a.GetTypes())
            .Where(t => !t.IsNested && !t.IsInterface && t.Name.EndsWith("Query", StringComparison.Ordinal)
                        && typeof(IBaseRequest).IsAssignableFrom(t)
                        && (t.Namespace == null || !t.Namespace.EndsWith(".Queries", StringComparison.Ordinal)))
            .Select(t => t.FullName)
            .ToArray();

        Assert.True(violations.Length == 0,
            $"*Query requests must live in a Queries folder: {string.Join(", ", violations)}");
    }
}
