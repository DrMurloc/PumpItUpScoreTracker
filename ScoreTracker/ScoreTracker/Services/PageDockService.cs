using Microsoft.AspNetCore.Components;

namespace ScoreTracker.Web.Services
{
    /// <summary>
    ///     The page action dock (UX rule 10, docs/design/randomizer-overhaul.md): pages
    ///     register thumb-reachable primary actions that MainLayout renders above the
    ///     mobile bottom nav; the nav itself slides away on scroll-down so both bars only
    ///     coexist at rest. Focus mode drops the shell chrome entirely for takeover tasks
    ///     (kiosk-style flows like tournament drafts) — a page requesting it owes the user
    ///     an explicit exit affordance in its own markup.
    ///     Scoped per circuit. Pages register through the PageDock component, never this
    ///     service directly; a page that registers nothing leaves the shell untouched.
    /// </summary>
    public sealed class PageDockService
    {
        public RenderFragment? DockContent { get; private set; }
        public bool FocusMode { get; private set; }

        public event Action? Changed;

        public void Set(RenderFragment? dockContent, bool focusMode)
        {
            DockContent = dockContent;
            FocusMode = focusMode;
            Changed?.Invoke();
        }

        public void Clear()
        {
            Set(null, false);
        }
    }
}
