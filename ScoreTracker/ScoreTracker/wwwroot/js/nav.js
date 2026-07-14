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

    // ===== More sheet =====

    function sheetIsOpen() {
        var sheet = document.querySelector('[data-more-sheet]');
        return !!sheet && sheet.classList.contains('open');
    }

    function setSheet(open) {
        var sheet = document.querySelector('[data-more-sheet]');
        if (!sheet) return;
        sheet.classList.toggle('open', open);
        sheet.setAttribute('aria-hidden', String(!open));
        var button = document.querySelector('[data-more-btn]');
        if (button) button.setAttribute('aria-expanded', String(open));
        document.documentElement.classList.toggle('sheet-open', open);
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

        if (el(target, '[data-more-btn]')) {
            e.preventDefault();
            setSheet(!sheetIsOpen());
            return;
        }

        // A <summary> drives disclosure inside a panel — it must not close what contains it.
        var isSummary = !!el(target, 'summary');

        if (openMenu && !isSummary && (!el(target, '[data-menu]') || el(target, 'a'))) closeMenu();

        if (sheetIsOpen() && !isSummary) {
            if (el(target, '[data-sheet-scrim]') || el(target, 'a')) setSheet(false);
        }
    }

    function onKeyDown(e) {
        if (e.key !== 'Escape') return;
        if (openMenu) {
            var activator = openMenu.querySelector('[data-menu-activator]');
            closeMenu();
            if (activator) activator.focus();
        } else if (sheetIsOpen()) {
            setSheet(false);
            var button = document.querySelector('[data-more-btn]');
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
        refreshActiveNav: refreshActiveNav,
        closeMenus: function () {
            closeMenu();
            setSheet(false);
        }
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
