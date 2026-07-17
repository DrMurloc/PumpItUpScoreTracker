namespace ScoreTracker.OfficialMirror.Contracts;

/// <summary>A detected likely rename awaiting the admin's accept/dismiss call.</summary>
[ExcludeFromCodeCoverage]
public sealed record RenameProposalRecord(int Id, string OldUsername, string NewUsername, bool AvatarMatched,
    int Top50Overlap);
