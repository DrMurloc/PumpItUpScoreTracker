using Bunit;
using Microsoft.AspNetCore.Components;

namespace ScoreTracker.Tests.Components;

internal static class BunitInteractive
{
    /// <summary>
    ///     Declares the render world for a test as an interactive circuit. SongImage, UserLabel,
    ///     ScoreBreakdown, PeerScore and ProjectionSpread gate their tooltips and popovers on
    ///     <c>RendererInfo.IsInteractive</c> — a popover throws in static SSR — and bUnit leaves
    ///     RendererInfo unset, so any test rendering them (directly or nested) must say which
    ///     world it is in. These components under test are exercised on their interactive
    ///     path, where the tooltip is live.
    /// </summary>
    internal static void RenderInteractive(this TestContext context)
    {
        context.Renderer.SetRendererInfo(new RendererInfo("Server", true));
    }
}
