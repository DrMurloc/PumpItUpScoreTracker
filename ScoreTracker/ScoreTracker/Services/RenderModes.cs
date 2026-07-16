using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace ScoreTracker.Web.Services;

/// <summary>
///     The one interactive render mode this app uses: server circuits, never prerendered.
///     Prerendering renders everything twice — a second OnInitializedAsync, JS interop
///     before the circuit exists — and is the app-wide flip that broke this site once
///     already (docs/design/render-modes.md). Pages opt in with
///     <c>@rendermode RenderModes.Interactive</c> and convert to static SSR by deleting
///     that line.
/// </summary>
public static class RenderModes
{
    public static readonly IComponentRenderMode Interactive = new InteractiveServerRenderMode(prerender: false);
}
