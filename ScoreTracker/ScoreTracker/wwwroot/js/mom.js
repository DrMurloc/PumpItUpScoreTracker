// March of Murlocs page glue: the Past Seasons <dialog>. The Season page is static SSR and
// the other MoM pages run circuits; both load this file, so everything binds by delegation
// on the document and no-ops on pages without the dialog.
(function () {
    "use strict";

    function dialog() { return document.getElementById("mom-seasons-dialog"); }

    document.addEventListener("click", function (e) {
        if (!e.target.closest) return;

        if (e.target.closest("[data-mom-seasons-open]")) {
            var dlg = dialog();
            if (dlg && !dlg.open) dlg.showModal();
            return;
        }

        if (e.target.closest("[data-mom-seasons-close]")) {
            var open = dialog();
            if (open && open.open) open.close();
            return;
        }

        // A click on the backdrop lands on the <dialog> element itself but outside its box;
        // Esc already closes natively, this makes the backdrop do the same.
        var d = dialog();
        if (d && d.open && e.target === d) {
            var r = d.getBoundingClientRect();
            if (e.clientX < r.left || e.clientX > r.right || e.clientY < r.top || e.clientY > r.bottom)
                d.close();
        }
    });
})();
