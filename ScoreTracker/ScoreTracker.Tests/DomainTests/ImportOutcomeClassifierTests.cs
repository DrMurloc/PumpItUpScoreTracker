using System;
using System.IO;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Threading.Tasks;
using ScoreTracker.OfficialMirror.Contracts;
using ScoreTracker.OfficialMirror.Domain;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public sealed class ImportOutcomeClassifierTests
{
    [Fact]
    public void AResetConnectionIsPiuGamesFault()
    {
        Assert.Equal(ImportOutcome.PiuGameError,
            ImportOutcomeClassifier.For(new HttpRequestException("The SSL connection could not be established.")));
    }

    [Fact]
    public void ARawSocketFailureIsPiuGamesFault()
    {
        Assert.Equal(ImportOutcome.PiuGameError,
            ImportOutcomeClassifier.For(new SocketException(10060)));
    }

    /// <summary>
    ///     The exact shape of the failure that produced the "tag updated, no scores" report:
    ///     HttpClient's own request timeout, which arrives as a TaskCanceledException wrapping a
    ///     TimeoutException — indistinguishable by type from a deliberate cancellation, which is
    ///     why the consumer rules ours out before asking this question.
    /// </summary>
    [Fact]
    public void AnHttpClientTimeoutIsPiuGamesFault()
    {
        var timeout = new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout",
            new TimeoutException());

        Assert.Equal(ImportOutcome.PiuGameError, ImportOutcomeClassifier.For(timeout));
    }

    /// <summary>
    ///     HttpClient wraps the real cause, so reading only the outermost type would call the most
    ///     common genuine failure ours and send the player a message blaming the wrong site.
    /// </summary>
    [Fact]
    public void ARemoteFailureBuriedUnderOurOwnExceptionIsStillPiuGamesFault()
    {
        var buried = new InvalidOperationException("Import failed",
            new HttpRequestException("Connection reset", new SocketException(10054)));

        Assert.Equal(ImportOutcome.PiuGameError, ImportOutcomeClassifier.For(buried));
    }

    [Fact]
    public void AStreamThatDiedMidResponseIsPiuGamesFault()
    {
        Assert.Equal(ImportOutcome.PiuGameError,
            ImportOutcomeClassifier.For(new IOException("The response ended prematurely.")));
    }

    [Fact]
    public void ABugInOurOwnCodeIsOurs()
    {
        Assert.Equal(ImportOutcome.PiuScoresError,
            ImportOutcomeClassifier.For(new NullReferenceException()));
    }

    [Fact]
    public void ADatabaseFailureIsOurs()
    {
        Assert.Equal(ImportOutcome.PiuScoresError,
            ImportOutcomeClassifier.For(new InvalidOperationException("The connection pool has been exhausted.")));
    }

    /// <summary>
    ///     InvalidCredentialException derives from AuthenticationException, which is also what a TLS
    ///     handshake failure throws. Classifying the base type as remote would quietly relabel "your
    ///     password is wrong" as "piugame is down" if that exception ever reached here — so it does
    ///     not, and this pins that.
    /// </summary>
    [Fact]
    public void ACredentialRejectionIsNotTreatedAsARemoteOutage()
    {
        Assert.Equal(ImportOutcome.PiuScoresError,
            ImportOutcomeClassifier.For(new InvalidCredentialException("Could not log in user to PIUgame")));
    }
}
