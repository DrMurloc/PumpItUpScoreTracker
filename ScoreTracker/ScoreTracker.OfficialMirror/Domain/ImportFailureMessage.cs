using ScoreTracker.OfficialMirror.Contracts;

namespace ScoreTracker.OfficialMirror.Domain;

/// <summary>
///     The sentence a failed run says to the player while they are still watching. One line per
///     outcome, and never anything a stack trace touched — this reaches a screen, so the rule from
///     DiagnosticExposureTests binds here even though these strings live in the Domain.
///     <para>
///         Deliberately plain English rather than a resource key: ImportStatusErrorEvent already
///         carries raw sentences to the page ("Invalid Login Information"), and inventing a second,
///         localized channel for the same event is a bigger change than this one is. The strip on
///         the page — the surface a player actually reads afterwards — is fully localized.
///     </para>
/// </summary>
internal static class ImportFailureMessage
{
    public static string For(ImportOutcome outcome)
    {
        return outcome switch
        {
            ImportOutcome.PiuGameError =>
                "PIUGame.com stopped responding, so this import couldn't finish. Try again in a few minutes.",
            ImportOutcome.CredentialRejected => "Invalid Login Information",
            _ => "This import couldn't finish. The error has been logged — try again shortly."
        };
    }
}
