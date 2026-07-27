// Shell chrome behavior (docs/design/static-shell.md): the nav is server-rendered HTML that
// never re-renders, so menu state, the More sheet and the bottom nav's active slot are owned
// here. MainLayout calls in through window.shell; nothing calls back out.
(function () {
    var MARGIN = 8; // px of breathing room a menu panel keeps from the viewport edge
    var openMenu = null;

    function el(target, selector) {
        return target && target.closest ? target.closest(selector) : null;
    }

    // ===== Menus =====

    // Panels sit absolutely under their activator, so a wide one near the right edge would
    // overflow the viewport. Shift it back on-screen, never past the left edge.
    function positionMenu(menu) {
        var panel = menu.querySelector('[data-menu-panel]');
        if (!panel) return;
        panel.style.left = '';
        var overflow = panel.getBoundingClientRect().right - (window.innerWidth - MARGIN);
        if (overflow <= 0) return;
        var room = Math.max(0, menu.getBoundingClientRect().left - MARGIN);
        panel.style.left = -Math.min(overflow, room) + 'px';
    }

    function setMenu(menu, open) {
        menu.classList.toggle('open', open);
        var activator = menu.querySelector('[data-menu-activator]');
        if (activator) activator.setAttribute('aria-expanded', String(open));
    }

    function closeMenu() {
        if (!openMenu) return;
        setMenu(openMenu, false);
        openMenu = null;
    }

    function showMenu(menu) {
        closeMenu();
        setMenu(menu, true);
        openMenu = menu;
        positionMenu(menu);
    }

    // ===== Sheets (More, Search) =====

    // Scrim-backed panels, at most one open at a time — they share the scrim, so two at once
    // would fight over it. More rises from the bottom for thumb reach; Search drops from the
    // top, because the keyboard takes the bottom half the moment its input focuses.
    // Both are static shell HTML; the search sheet's autocomplete is an island INSIDE it,
    // which is why the chrome opens from here and not from a circuit.
    var SEARCH_SHEET = { sheet: '[data-search-sheet]', button: '[data-search-btn]', focus: 'input' };
    var SHEETS = [
        { sheet: '[data-more-sheet]', button: '[data-more-btn]' },
        SEARCH_SHEET
    ];

    function sheetNode(spec) {
        return document.querySelector(spec.sheet);
    }

    function openSheet() {
        for (var i = 0; i < SHEETS.length; i++) {
            var node = sheetNode(SHEETS[i]);
            if (node && node.classList.contains('open')) return SHEETS[i];
        }
        return null;
    }

    function setSheet(spec, open) {
        var node = sheetNode(spec);
        if (!node) return;
        node.classList.toggle('open', open);
        node.setAttribute('aria-hidden', String(!open));
        // Reopening should land on the root list, not wherever the last visit drilled to.
        if (!open) resetDrill(node.querySelector('[data-more-nav]'));
        var button = document.querySelector(spec.button);
        if (button) button.setAttribute('aria-expanded', String(open));
        // Read back rather than trust `open`: the flag means "some sheet is up".
        document.documentElement.classList.toggle('sheet-open', !!openSheet());
        if (open && spec.focus) focusInto(node, spec.focus);
    }

    // A sheet that exists to be typed into should be ready to type into. This has to run in
    // the opening click's OWN call stack: iOS raises the keyboard only for a focus() it can
    // trace back to a user gesture, so deferring it behind a timeout or the slide-in
    // transition would open the sheet and leave the field cold.
    // The field can legitimately be missing — it belongs to an island, and prerendering is
    // off, so a tap landing before the circuit connects finds nothing to focus.
    function focusInto(node, selector) {
        var field = node.querySelector(selector);
        if (field) field.focus();
    }

    function closeSheets() {
        var spec = openSheet();
        if (spec) setSheet(spec, false);
    }

    // ===== More sheet: drill-down =====

    // The sheet ships one grouped list. Where there is horizontal room the CSS lays it out as
    // an icon grid and this module stays asleep; on a narrow, tall phone there is no such
    // room, so the groups collapse to a root list of section names and open one at a time.
    //
    // The media query is the one the shell already uses for squarish viewports (the bottom
    // nav's wide-only slot), plus a width floor for portrait tablets — those are TALLER than
    // 1:1 but have plenty of width, and would otherwise get the phone treatment across 768px.
    var GRID_QUERY = '(min-aspect-ratio: 1/1), (min-width: 700px)';

    function moreNav() {
        return document.querySelector('[data-more-nav]');
    }

    function isGridLayout() {
        return window.matchMedia(GRID_QUERY).matches;
    }

    // Root: heads are rows, bodies are away, back bar is gone.
    // Opened: one body showing, everything else away, back bar naming where you are.
    function showGroup(nav, group) {
        var back = nav.querySelector('[data-more-back]');
        var label = nav.querySelector('[data-more-back-label]');
        var groups = nav.querySelectorAll('[data-more-group]');

        for (var i = 0; i < groups.length; i++) {
            var isTarget = groups[i] === group;
            groups[i].classList.toggle('drill-open', isTarget);
            groups[i].hidden = !!group && !isTarget;
            var head = groups[i].querySelector('[data-more-group-head]');
            if (head && head.hasAttribute('role')) head.setAttribute('aria-expanded', String(isTarget));
        }
        // Ungrouped rows (About, the gated-mix import link) belong to the root only.
        nav.classList.toggle('drill-inside', !!group);

        if (back) back.hidden = !group;
        if (label && group) {
            var heading = group.querySelector('[data-more-group-head] span');
            label.textContent = heading ? heading.textContent : '';
        }
        // The sheet is the scroll container, not this list — coming back from a long
        // section otherwise leaves the root scrolled to where that section ended.
        var sheet = el(nav, '[data-more-sheet]');
        if (sheet) sheet.scrollTop = 0;
    }

    function resetDrill(nav) {
        var target = nav || moreNav();
        if (target) showGroup(target, null);
    }

    // A head is a label in grid mode and a control in drill mode. It only gets a role and a
    // tab stop in the mode where it actually does something — otherwise keyboard users tab
    // onto five inert rows.
    function syncDrillAffordances() {
        var nav = moreNav();
        if (!nav) return;
        var grid = isGridLayout();
        var heads = nav.querySelectorAll('[data-more-group-head]');

        for (var i = 0; i < heads.length; i++) {
            if (grid) {
                heads[i].removeAttribute('role');
                heads[i].removeAttribute('tabindex');
                heads[i].removeAttribute('aria-expanded');
            } else {
                heads[i].setAttribute('role', 'button');
                heads[i].setAttribute('tabindex', '0');
                heads[i].setAttribute('aria-expanded', 'false');
            }
        }
        nav.classList.toggle('drill-mode', !grid);
        // Unfolding a phone mid-session swaps the layout underneath an open drill panel;
        // the grid has no notion of "inside a group", so it has to start from the root.
        if (grid) resetDrill(nav);
    }

    function onDrillClick(target) {
        var nav = moreNav();
        if (!nav || isGridLayout()) return false;

        if (el(target, '[data-more-back]')) {
            resetDrill(nav);
            return true;
        }
        var head = el(target, '[data-more-group-head]');
        if (head && nav.contains(head)) {
            var group = el(head, '[data-more-group]');
            if (group) showGroup(nav, group);
            return true;
        }
        return false;
    }

    // ===== Bottom nav active slot =====

    // The server renders the active slot from the request path; this keeps it honest across
    // Blazor's client-side navigations, which the shell never sees. Same prefix rules: "/"
    // matches only itself, every other slot matches its own subtree.
    function refreshActiveNav() {
        var path = location.pathname.replace(/\/+$/, '') || '/';
        var slots = document.querySelectorAll('.bottom-nav .bn[data-href]');
        for (var i = 0; i < slots.length; i++) {
            var href = slots[i].getAttribute('data-href');
            var active = href === '/'
                ? path === '/'
                : path.toLowerCase().indexOf(href.toLowerCase()) === 0;
            slots[i].classList.toggle('active', active);
        }
    }

    // Blazor and the pages navigate through pushState/replaceState, which fire no event of
    // their own. A push is a step to somewhere else, so it closes any open sheet — a sheet is
    // chrome over the page it was opened from, and the click handler cannot catch the search
    // autocomplete, which navigates from a circuit without a link. A replace is the page
    // rewriting its own URL in place (the tier list canonicalizing /TierLists into its folder
    // route), so the sheet stays up.
    function wrapHistory(name, leavesPage) {
        var original = history[name];
        if (typeof original !== 'function') return;
        history[name] = function () {
            var result = original.apply(this, arguments);
            refreshActiveNav();
            if (leavesPage) closeSheets();
            return result;
        };
    }

    // ===== Events =====

    function onClick(e) {
        var target = e.target;

        var activator = el(target, '[data-menu-activator]');
        if (activator) {
            e.preventDefault();
            var menu = el(activator, '[data-menu]');
            if (menu === openMenu) closeMenu();
            else if (menu) showMenu(menu);
            return;
        }

        for (var i = 0; i < SHEETS.length; i++) {
            if (!el(target, SHEETS[i].button)) continue;
            e.preventDefault();
            var node = sheetNode(SHEETS[i]);
            var wasOpen = !!node && node.classList.contains('open');
            closeSheets();
            if (!wasOpen) setSheet(SHEETS[i], true);
            return;
        }

        // Drilling into a section is movement WITHIN the sheet, so it must not close it.
        if (onDrillClick(target)) {
            e.preventDefault();
            return;
        }

        // A <summary> drives disclosure inside a panel — it must not close what contains it.
        // The More sheet's own disclosures are gone, but the mix picker still uses one.
        var isSummary = !!el(target, 'summary');

        if (openMenu && !isSummary && (!el(target, '[data-menu]') || el(target, 'a'))) closeMenu();

        var open = openSheet();
        if (open && !isSummary) {
            if (el(target, '[data-sheet-scrim]') || el(target, 'a')) setSheet(open, false);
        }
    }

    function onKeyDown(e) {
        // A head carries role="button" only in drill mode, so it owes the keyboard the
        // activation a real button would have given for free.
        if (e.key === 'Enter' || e.key === ' ') {
            var head = el(e.target, '[data-more-group-head]');
            if (head && !isGridLayout() && onDrillClick(e.target)) e.preventDefault();
            return;
        }

        if (e.key !== 'Escape') return;

        // Inside a section, Escape is "back" before it is "close" — the sheet is still the
        // thing you meant to be in.
        var nav = moreNav();
        if (nav && nav.classList.contains('drill-inside')) {
            resetDrill(nav);
            return;
        }

        if (openMenu) {
            var activator = openMenu.querySelector('[data-menu-activator]');
            closeMenu();
            if (activator) activator.focus();
            return;
        }

        var spec = openSheet();
        if (spec) {
            setSheet(spec, false);
            // Focus goes back to whichever button opened it, not always More.
            var button = document.querySelector(spec.button);
            if (button) button.focus();
        }
    }

    function onResize() {
        if (openMenu) positionMenu(openMenu);
    }

    // ===== Public surface (MainLayout, ShellImportPulse and static pages call these) =====

    window.shell = {
        // The two search bars are viewport-exclusive: phones get the sheet (which focuses
        // its own input), desktop's search already sits in the app bar so opening it just
        // means focusing it. Must run in the caller's click stack for the iOS keyboard.
        openSearch: function () {
            // Geometry, not computed display: the bottom nav hides by display:none on the
            // WRAPPER, which leaves the button's own computed display untouched — but a
            // hidden ancestor zeroes the rect. Opening the sheet on desktop shows only its
            // scrim (the sheet itself is mobile-only): a dark screen and nothing else.
            var button = document.querySelector(SEARCH_SHEET.button);
            if (button && button.getBoundingClientRect().width > 0) {
                closeSheets();
                setSheet(SEARCH_SHEET, true);
                return;
            }
            var field = document.querySelector('.shell-appbar .appbar-search input');
            if (!field) return;
            field.focus();
            // Focus alone is easy to miss up in the app bar — flash the field once.
            var wrap = field.closest('.appbar-search');
            if (!wrap) return;
            wrap.classList.remove('search-flash');
            void wrap.offsetWidth; // restart the animation on a second click
            wrap.classList.add('search-flash');
            wrap.addEventListener('animationend', function done() {
                wrap.classList.remove('search-flash');
                wrap.removeEventListener('animationend', done);
            });
        },
        // Dock and focus classes live on <html> because the shell that reacts to them is
        // outside the Blazor root that knows about them.
        setDockState: function (hasDock, focusMode) {
            var root = document.documentElement;
            root.classList.toggle('has-dock', !!hasDock);
            root.classList.toggle('focus-mode', !!focusMode);
        },
        setImportPulse: function (running) {
            var dot = document.getElementById('shell-import-pulse');
            if (dot) dot.hidden = !running;
        },
        refreshActiveNav: refreshActiveNav
    };

    function init() {
        document.addEventListener('click', onClick);
        document.addEventListener('keydown', onKeyDown);
        window.addEventListener('resize', onResize);
        window.addEventListener('popstate', refreshActiveNav);
        wrapHistory('pushState', true);
        wrapHistory('replaceState', false);
        // A fold phone changes viewport mid-session, so the sheet has to re-decide its
        // layout live rather than once at load. matchMedia fires on the transition itself;
        // resize would too, but this only wakes on the boundary that matters.
        var gridWatch = window.matchMedia(GRID_QUERY);
        if (gridWatch.addEventListener) gridWatch.addEventListener('change', syncDrillAffordances);
        else if (gridWatch.addListener) gridWatch.addListener(syncDrillAffordances);
        syncDrillAffordances();
        // The dock's scroll watcher needs no circuit, so it starts with the page.
        if (window.pageDock) window.pageDock.watch();
        refreshActiveNav();
    }

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
    else init();
})();
