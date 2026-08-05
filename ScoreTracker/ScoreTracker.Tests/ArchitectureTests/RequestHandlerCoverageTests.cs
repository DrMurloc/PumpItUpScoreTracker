using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using MediatR;
using Xunit;

namespace ScoreTracker.Tests.ArchitectureTests;

/// <summary>
///     Every MediatR request must have a handler, and every handler must say so on its class.
///     <para>
///         Born from a real outage on this branch: a saga grew a
///         <c>public Task&lt;int&gt; Handle(SaveOfficialScoresCommand, CancellationToken)</c> method
///         but the matching <c>IRequestHandler&lt;,&gt;</c> never made it onto the class declaration.
///         That compiles — a method named Handle needs no interface — every suite stayed green
///         because component tests mock <c>IMediator</c>, and the feature threw
///         "No service for type IRequestHandler`2 has been registered" the first time a player
///         pressed the button. This is the generic form of the CommunityTools lesson: nothing else
///         checks that a request can actually be dispatched.
///     </para>
/// </summary>
public sealed class RequestHandlerCoverageTests
{
    private static readonly Assembly[] Assemblies =
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
        typeof(CommunityTools.Contracts.Commands.CreateToolCommand).Assembly,
        typeof(HomePage.Contracts.Commands.CreateHomePageCommand).Assembly,
        typeof(Identity.Contracts.Commands.CreateUserCommand).Assembly,
        typeof(Translations.Contracts.Commands.TranslateCommentCommand).Assembly
    };

    /// <summary>
    ///     Requests deliberately dispatched from outside these assemblies, or kept without a
    ///     handler on purpose. Adding a row here is a decision, which is the point of the list.
    /// </summary>
    private static readonly Dictionary<string, string> Exempt = new()
    {
        // All four were handled by Application/Handlers/MatchSaga.cs, and 406c03e5 "Delete the
        // Match application layer" removed the saga while leaving the records behind. Their
        // senders were already gone by then, so nothing constructs or handles them now. Listed
        // rather than deleted here to keep an unrelated cleanup out of the Score check branch.
        // (The randomizer overhaul is only where the live replacements landed —
        // SaveTournamentRandomSettingsCommand, GetTournamentRandomSettingsQuery,
        // DrawRandomChartsQuery — which is easy to mistake for these being its leftovers.)
        ["DrawChartsCommand"] = "Orphaned by 406c03e5 (Match layer deleted) — no sender, no handler.",
        ["FinishCardDrawCommand"] = "Orphaned by 406c03e5 (Match layer deleted) — no sender, no handler.",
        ["SaveRandomSettingsCommand"] = "Orphaned by 406c03e5 (Match layer deleted) — no sender, no handler.",
        ["GetAllRandomSettingsQuery"] = "Orphaned by 406c03e5 (Match layer deleted) — no sender, no handler."
    };

    private static Type[] AllTypes()
    {
        return Assemblies.SelectMany(a =>
        {
            try
            {
                return a.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                return e.Types.Where(t => t != null).Select(t => t!).ToArray();
            }
        }).Where(t => !t.IsNested && !t.Name.Contains('<')).ToArray();
    }

    [Fact]
    public void EveryRequestHasAHandler()
    {
        var types = AllTypes();
        var handled = types
            .Where(t => t is { IsAbstract: false, IsInterface: false })
            .SelectMany(t => t.GetInterfaces())
            .Where(i => i.IsGenericType && (i.GetGenericTypeDefinition() == typeof(IRequestHandler<>)
                                            || i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>)))
            .Select(i => i.GetGenericArguments()[0])
            .ToHashSet();

        var unhandled = types
            .Where(t => t is { IsAbstract: false, IsInterface: false } && typeof(IBaseRequest).IsAssignableFrom(t))
            .Where(t => !handled.Contains(t) && !Exempt.ContainsKey(t.Name))
            .Select(t => t.FullName!)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        Assert.True(unhandled.Length == 0,
            "These requests can be sent but nothing handles them, so the first dispatch throws at " +
            "run time and no other suite notices — component tests mock IMediator. Implement " +
            $"IRequestHandler<> on the handling class, or add an Exempt row saying why not: {string.Join(", ", unhandled)}");
    }

    [Fact]
    public void AHandleMethodWithoutItsInterfaceIsNotAHandler()
    {
        // The specific shape that caused the outage: the method is there, the interface is not, so
        // MediatR's assembly scan never sees it.
        var offenders = new List<string>();
        foreach (var type in AllTypes().Where(t => t is { IsAbstract: false, IsInterface: false }))
        {
            var declared = type.GetInterfaces()
                .Where(i => i.IsGenericType && (i.GetGenericTypeDefinition() == typeof(IRequestHandler<>)
                                                || i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>)))
                .Select(i => i.GetGenericArguments()[0])
                .ToHashSet();

            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance |
                                                   BindingFlags.DeclaredOnly)
                         .Where(m => m.Name == "Handle"))
            {
                var parameters = method.GetParameters();
                if (parameters.Length != 2 || parameters[1].ParameterType != typeof(CancellationToken)) continue;

                var requestType = parameters[0].ParameterType;
                if (!typeof(IBaseRequest).IsAssignableFrom(requestType)) continue;
                if (declared.Contains(requestType)) continue;

                offenders.Add($"{type.FullName}.Handle({requestType.Name})");
            }
        }

        Assert.True(offenders.Count == 0,
            "These classes handle a MediatR request by method but do not implement the matching " +
            "IRequestHandler<> interface, so MediatR's scan never registers them and the dispatch " +
            $"throws at run time: {string.Join(", ", offenders)}");
    }
}
