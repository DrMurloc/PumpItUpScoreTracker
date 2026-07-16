using Bunit;
using Microsoft.AspNetCore.Components;

namespace ScoreTracker.Tests.Components;

internal static class BunitInteractive
{
    /// <summary>
    ///     Declares the render world for a test as an interactive circuit. DifficultyBubble
    ///     and SongImage gate their MudTooltip on <c>RendererInfo.IsInteractive</c> — the
    ///     tooltip renders a popover, which throws in static SSR — and bUnit leaves
    ///     RendererInfo unset, so any test rendering them (directly or nested) must say which
    ///     world it is in. These components under test are exercised on their interactive
    ///     path, where the tooltip is live.
    /// </summary>
    internal static void RenderInteractive(this TestContext context)
    {
        context.Renderer.SetRendererInfo(new RendererInfo("Server", true));
    }
}
