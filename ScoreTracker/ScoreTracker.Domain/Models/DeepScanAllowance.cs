namespace ScoreTracker.Domain.Models;

/// <summary>
///     How many full-account walks of the official site an account may run per month. Lives in
///     Domain because three places need the same number and none of them may own it: the User row
///     starts there, the monthly reset refills to it, and the migration back-fills existing rows
///     with it. A second copy would drift and nothing would notice until a player ran out early.
/// </summary>
public static class DeepScanAllowance
{
    public const int PerMonth = 3;
}
