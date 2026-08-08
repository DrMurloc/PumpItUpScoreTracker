namespace ScoreTracker.Domain.Exceptions
{
    /// <summary>
    ///     A comment action the domain refuses: a body over the cap, a reply to somebody else's
    ///     audience, a vote on your own words, an edit of a comment that is not yours.
    ///     <para>
    ///         Lives here rather than inside the vertical for the reason
    ///         <see cref="CommunityPermissionException" /> does — Web catches it by type to show the
    ///         reason, and the reason is written to be read by a player, which is what keeps it on
    ///         the right side of <c>DiagnosticExposureTests</c>.
    ///     </para>
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed class CommentNotAllowedException : Exception
    {
        public CommentNotAllowedException(string reason) : base(reason)
        {
        }
    }
}
