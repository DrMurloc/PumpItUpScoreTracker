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
    var SHEETS = [
        { sheet: '[data-more-sheet]', button: '[data-more-btn]' },
        { sheet: '[data-search-sheet]', button: '[data-search-btn]' }
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
        var button = document.querySelector(spec.button);
        if (button) button.setAttribute('aria-expanded', String(open));
        // Read back rather than trust `open`: the flag means "some sheet is up".
        document.documentElement.classList.toggle('sheet-open', !!openSheet());
    }

    function closeSheets() {
        var spec = openSheet();
        if (spec) setSheet(spec, false);
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

    // Blazor navigates through pushState/replaceState, which fire no event of their own.
    function wrapHistory(name) {
        var original = history[name];
        if (typeof original !== 'function') return;
        history[name] = function () {
            var result = original.apply(this, arguments);
            refreshActiveNav();
            // A sheet is chrome over the page it was opened from, so leaving that page
            // closes it. The click handler catches links; it cannot catch the search
            // autocomplete, which navigates from a circuit without one.
            closeSheets();
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

        // A <summary> drives disclosure inside a panel — it must not close what contains it.
        var isSummary = !!el(target, 'summary');

        if (openMenu && !isSummary && (!el(target, '[data-menu]') || el(target, 'a'))) closeMenu();

        var open = openSheet();
        if (open && !isSummary) {
            if (el(target, '[data-sheet-scrim]') || el(target, 'a')) setSheet(open, false);
        }
    }

    function onKeyDown(e) {
        if (e.key !== 'Escape') return;
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

    // ===== Public surface (MainLayout and ShellImportPulse call these) =====

    window.shell = {
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
        wrapHistory('pushState');
        wrapHistory('replaceState');
        // The dock's scroll watcher needs no circuit, so it starts with the page.
        if (window.pageDock) window.pageDock.watch();
        refreshActiveNav();
    }

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
    else init();
})();
