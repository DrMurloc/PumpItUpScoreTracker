using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Web.Services;

/// <summary>
///     Request-resolved shell state, carried into a circuit that can no longer see the request.
///     The shell resolves the mix while the HttpContext is live and seeds this; App re-seeds it
///     from its root parameter once the circuit starts, so nothing inside the circuit re-derives
///     request state. Scoped, so it never crosses circuits.
/// </summary>
public sealed class ShellContext
{
    public MixEnum? CurrentMix { get; set; }
}
