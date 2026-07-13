using Microsoft.AspNetCore.Components;

namespace ScoreTracker.Web.Services.HomeDashboard;

/// <summary>
///     A live channel from a widget into the host's title bar. The host owns the card
///     chrome (§2.3), but a widget may push a small fragment — e.g. the selected chart's
///     art — into the header to reclaim body space. Opt-in: a widget consumes it via a
///     <c>[CascadingParameter]</c>; widgets that ignore it are unaffected, and the host
///     renders nothing until something is set. Setting content asks the host to re-render.
/// </summary>
public sealed class WidgetHeaderSlot
{
    private readonly Action _notify;

    public WidgetHeaderSlot(Action notify)
    {
        _notify = notify;
    }

    public RenderFragment? Content { get; private set; }

    public void Set(RenderFragment? content)
    {
        // Clearing an already-empty slot is a no-op — keeps a widget's search-state
        // re-renders from churning the host.
        if (Content == null && content == null) return;
        Content = content;
        _notify();
    }
}
