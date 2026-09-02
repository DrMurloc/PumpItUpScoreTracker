using System;
using Discord;
using Microsoft.Extensions.Logging;
using ScoreTracker.Data.Clients;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class DiscordLogMappingTests
{
    [Theory]
    [InlineData(LogSeverity.Critical, LogLevel.Critical)]
    [InlineData(LogSeverity.Error, LogLevel.Error)]
    [InlineData(LogSeverity.Warning, LogLevel.Warning)]
    [InlineData(LogSeverity.Info, LogLevel.Information)]
    [InlineData(LogSeverity.Verbose, LogLevel.Debug)]
    [InlineData(LogSeverity.Debug, LogLevel.Trace)]
    public void EverySeverityMapsOntoTheLoggerLevel(LogSeverity severity, LogLevel expected)
    {
        Assert.Equal(expected, DiscordLogMapping.ToLogLevel(severity));
    }

    [Fact]
    public void AnExceptionOnlyEntryReadsAsTheExceptionMessage()
    {
        // Discord.Net's ConnectionManager logs a dropped connection as WarningAsync(ex): no
        // message, just the exception. This is the entry that used to render as "[null]".
        var entry = new LogMessage(LogSeverity.Warning, "Gateway", null,
            new Exception("WebSocket connection was closed"));

        Assert.Equal("WebSocket connection was closed", DiscordLogMapping.Text(entry));
    }

    [Fact]
    public void AnEmptyMessageFallsBackToTheExceptionToo()
    {
        var entry = new LogMessage(LogSeverity.Warning, "Gateway", string.Empty,
            new Exception("Server missed last heartbeat"));

        Assert.Equal("Server missed last heartbeat", DiscordLogMapping.Text(entry));
    }

    [Fact]
    public void AMessageWinsOverItsException()
    {
        var entry = new LogMessage(LogSeverity.Error, "Gateway", "Heartbeat Errored", new Exception("boom"));

        Assert.Equal("Heartbeat Errored", DiscordLogMapping.Text(entry));
    }

    [Fact]
    public void AnEmptyEntryYieldsAnEmptyString()
    {
        Assert.Equal(string.Empty, DiscordLogMapping.Text(new LogMessage(LogSeverity.Info, "Discord", null)));
    }
}
