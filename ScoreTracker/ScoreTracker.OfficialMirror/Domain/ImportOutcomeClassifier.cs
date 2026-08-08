using System.Net.Sockets;
using ScoreTracker.OfficialMirror.Contracts;

namespace ScoreTracker.OfficialMirror.Domain;

/// <summary>
///     Decides whose fault a failed import was. Deliberately a lookup over exception types rather
///     than a heuristic: everything thrown on the way out of the piugame client is theirs, and
///     everything else is ours, so there is exactly one question to answer per exception and no
///     judgement call at the call site.
///     <para>
///         Two piugame-side failures never reach here — InvalidCredentialException and
///         NoGameAccountAssociatedException are caught earlier and told to the player in their own
///         words, because "your password is wrong" is not "piugame is having a bad day".
///     </para>
///     <para>
///         Nor does a cancellation of OUR token: that is a shutdown, not a fault, and its run is
///         left unfinished on purpose. The distinction matters because an HttpClient timeout
///         arrives as the same TaskCanceledException — it is only telling the two apart by
///         <c>token.IsCancellationRequested</c> that makes a timeout classifiable at all.
///     </para>
/// </summary>
internal static class ImportOutcomeClassifier
{
    public static ImportOutcome For(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
            if (IsRemote(current))
                return ImportOutcome.PiuGameError;

        return ImportOutcome.PiuScoresError;
    }

    /// <summary>
    ///     The whole chain is walked, not just the outermost type: HttpClient wraps a
    ///     SocketException in an HttpRequestException, and reports its own request timeout as a
    ///     TaskCanceledException wrapping a TimeoutException. Reading only the top would classify
    ///     the most common real failure as ours.
    /// </summary>
    private static bool IsRemote(Exception exception)
    {
        return exception
            is HttpRequestException // reset connections, TLS failures, non-success status codes
            or SocketException // the raw connect failure underneath most of the above
            or IOException // a stream that died mid-response
            or TimeoutException
            or TaskCanceledException; // HttpClient's own request timeout, once ours is ruled out
    }
}
