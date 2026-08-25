// March of Murlocs page glue: the Past Seasons <dialog>, and the Planner's CSV download.
// The Season page is static SSR and the other MoM pages run circuits; both load this file,
// so everything binds by delegation on the document and no-ops on pages without the dialog.
(function () {
    "use strict";

    // The Planner's CSV walks to a machine client-side — no backend. The BOM is what makes
    // Excel open Korean and Japanese song titles as UTF-8 instead of mojibake (§11.5).
    window.momDownloadCsv = function (filename, text) {
        var blob = new Blob(["﻿" + text], { type: "text/csv;charset=utf-8" });
        var a = document.createElement("a");
        a.href = URL.createObjectURL(blob);
        a.download = filename;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        setTimeout(function () { URL.revokeObjectURL(a.href); }, 1000);
    };

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
