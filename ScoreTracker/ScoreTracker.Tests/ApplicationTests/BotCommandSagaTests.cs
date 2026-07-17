using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ScoreTracker.Communities.Application;
using ScoreTracker.Communities.Contracts.Commands;
using ScoreTracker.Communities.Contracts.Queries;
using ScoreTracker.Domain.Records;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class BotCommandSagaTests
{
    private static HandleBotInteractionCommand Invoke(string[] path, Dictionary<string, string> options) =>
        new(new BotInteraction(path, options, ChannelId: 100, GuildId: 200, UserId: 300,
            UserDisplayName: "Tester", InvokerCanManageChannels: false));

    [Fact]
    public async Task CalcReturnsAScoreBreakdownCarryingGradeAndPlateTokens()
    {
        var saga = new BotCommandSaga();

        var reply = await saga.Handle(Invoke(new[] { "calc" }, new Dictionary<string, string>
        {
            ["perfects"] = "950", ["greats"] = "40", ["goods"] = "5",
            ["bads"] = "3", ["misses"] = "2", ["combo"] = "900"
        }), CancellationToken.None);

        Assert.Null(reply.Card);
        Assert.NotNull(reply.Text);
        Assert.Contains("#LETTERGRADE|", reply.Text);
        Assert.Contains("#PLATE|", reply.Text);
        Assert.Contains("Lost to Greats", reply.Text);
    }

    [Fact]
    public async Task CalcEstimatesStepsWhenCaloriesAreProvided()
    {
        var saga = new BotCommandSaga();

        var reply = await saga.Handle(Invoke(new[] { "calc" }, new Dictionary<string, string>
        {
            ["perfects"] = "500", ["greats"] = "20", ["goods"] = "0",
            ["bads"] = "0", ["misses"] = "0", ["combo"] = "500", ["calories"] = "18"
        }), CancellationToken.None);

        Assert.Contains("Estimated Arrow Presses", reply.Text);
    }

    [Fact]
    public async Task CalcRejectsAnInvalidConfiguration()
    {
        var saga = new BotCommandSaga();

        // Max combo greater than the total note count can't happen — it's invalid.
        var reply = await saga.Handle(Invoke(new[] { "calc" }, new Dictionary<string, string>
        {
            ["perfects"] = "10", ["greats"] = "0", ["goods"] = "0",
            ["bads"] = "0", ["misses"] = "0", ["combo"] = "999"
        }), CancellationToken.None);

        Assert.Contains("invalid", reply.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AnUnbuiltCommandRepliesThatItIsNotAvailableYet()
    {
        var saga = new BotCommandSaga();

        var reply = await saga.Handle(Invoke(new[] { "chart" }, new Dictionary<string, string>()),
            CancellationToken.None);

        Assert.Contains("isn't available", reply.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AutocompleteReturnsNothingUntilLookupsAreWired()
    {
        var saga = new BotCommandSaga();

        var choices = await saga.Handle(new GetBotAutocompleteQuery(
            new BotAutocompleteRequest(new[] { "chart" }, "name", "ug",
                new Dictionary<string, string>(), UserId: 1, ChannelId: 2, GuildId: 3)), CancellationToken.None);

        Assert.Empty(choices);
    }
}
