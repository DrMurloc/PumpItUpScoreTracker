// Page dock scroll behavior (docs/design/randomizer-overhaul.md): scrolling down hides
// the mobile bottom nav (html.nav-away), scrolling up or returning to the top restores
// it. Pure class toggling — no .NET callbacks; CSS owns the animation.
(function () {
    var last = 0;
    var ticking = false;
    var watching = false;
    var THRESHOLD = 8; // px of travel before reacting — ignores rubber-band jitter

    function onScroll() {
        if (ticking) return;
        ticking = true;
        window.requestAnimationFrame(function () {
            var y = window.scrollY;
            var root = document.documentElement;
            if (y <= 0) {
                root.classList.remove('nav-away');
            } else if (y - last > THRESHOLD) {
                root.classList.add('nav-away');
            } else if (last - y > THRESHOLD) {
                root.classList.remove('nav-away');
            }
            last = y;
            ticking = false;
        });
    }

    window.pageDock = {
        watch: function () {
            if (watching) return;
            watching = true;
            last = window.scrollY;
            window.addEventListener('scroll', onScroll, { passive: true });
        },
        // Called on navigation: a fresh page starts with the nav visible.
        reset: function () {
            document.documentElement.classList.remove('nav-away');
            last = window.scrollY;
        }
    };
})();
