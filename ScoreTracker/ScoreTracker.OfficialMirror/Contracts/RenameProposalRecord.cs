namespace ScoreTracker.OfficialMirror.Contracts;

/// <summary>
///     One tag that left the boards, with the verdict and the evidence behind it. Rendered
///     for every vanished tag — including the ones that merged themselves and the ones that
///     simply got passed — because the whole population is what says whether the rule is
///     still catching what it should.
/// </summary>
/// <param name="Verdict">See <c>VanishVerdicts</c>: Merge, Ambiguous, Suspicious, Propose, DroppedOff.</param>
/// <param name="Status">See <c>ProposalStatuses</c>: what was done, and whether a human did it.</param>
/// <param name="ExactNonPgMatches">Identical scores that are not perfect games — what identifies a person.</param>
/// <param name="RunnerUpExactMatches">The next-best candidate's count, so a close call reads as one.</param>
/// <param name="SuspiciousAbsences">Boards where a score this tag held should still be ranking and nobody is.</param>
[ExcludeFromCodeCoverage]
public sealed record RenameProposalRecord(int Id, string OldUsername, string? NewUsername, string Verdict,
    string Status, int OldPlacements, int BoardsPresent, int ExactNonPgMatches, int ExactPerfectGames,
    int RunnerUpExactMatches, int SuspiciousAbsences, bool AvatarMatched);
