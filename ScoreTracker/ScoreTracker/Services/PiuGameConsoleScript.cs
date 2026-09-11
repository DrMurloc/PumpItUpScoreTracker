using System.Globalization;

namespace ScoreTracker.Web.Services;

/// <summary>
///     The paste-into-console scraper the manual import hands out
///     (docs/design/import-scores-refresh.md). It runs in the player's own signed-in tab on the
///     official site and fetches same-origin, so the site's session rides along; nothing reaches
///     PIU Scores until the player uploads the CSV it downloads.
///     <para>
///         It reads both best-list layouts the way <c>PiuGameApi.GetBestScores</c> does — the
///         classic Phoenix 1 list and the Phoenix 2 redesign, told apart by their markup — and it
///         is built for one site: pasted on the other it says so and stops, because a Phoenix 1
///         list uploaded into Phoenix 2 would resolve against the wrong catalog without an error.
///     </para>
///     <para>
///         It saves through a Blob and a temporary <c>&lt;a download&gt;</c>, since browsers block
///         top-frame data: navigation, and the file opens with a byte-order mark so Excel keeps
///         Korean and Japanese song names intact.
///     </para>
/// </summary>
public static class PiuGameConsoleScript
{
    /// <param name="siteBaseUrl">The selected mix's official site.</param>
    /// <param name="startPage">The first best-list page to read.</param>
    /// <param name="endPage">The last page to read; null reads to the end of the list.</param>
    public static string For(string siteBaseUrl, int startPage, int? endPage)
    {
        var site = new Uri(siteBaseUrl).Host;
        var first = Math.Max(1, startPage).ToString(CultureInfo.InvariantCulture);
        var last = endPage is { } end ? Math.Max(1, end).ToString(CultureInfo.InvariantCulture) : "null";
        return $$"""
        (async () => {
        const site = "{{site}}";
        if (location.hostname !== site && location.hostname !== "www." + site) {
            alert("This script reads your scores from " + site + ". Open https://" + site + ", sign in, and run it there.");
            return;
        }
        const firstPage = {{first}};
        const lastPage = {{last}};
        const read = async page => new DOMParser().parseFromString(
            await (await fetch("/my_page/my_best_score.php?page=" + page, { credentials: "include" })).text(), "text/html");
        const text = (node, selector) => ((node.querySelector(selector) || {}).textContent || "").trim();
        const src = img => (img && img.getAttribute("src")) || "";
        const cell = value => '"' + String(value).replaceAll('"', '""') + '"';
        let rows = "Song,Difficulty,Score,LetterGrade,Plate,IsBroken\r\n";
        let scoreCount = 0, skipped = 0, failedRows = 0, pageCount = 0, maxPage = firstPage;
        for (let page = firstPage; ; page++) {
            const doc = await read(page);
            const classic = doc.querySelector("ul.my_best_scoreList") !== null;
            const cards = doc.querySelectorAll(classic ? "ul.my_best_scoreList > li > div.in" : "ul.recently_playeList > li");
            if (page === firstPage) {
                if (cards.length === 0) {
                    alert("No scores found - sign in on https://" + site + " first, then run this again.");
                    return;
                }
                const pager = doc.querySelector("i.last");
                const button = pager === null ? null : pager.closest("[onclick]");
                const found = /page=(\d+)/.exec(button === null ? "" : button.getAttribute("onclick"));
                maxPage = found === null ? firstPage : parseInt(found[1], 10);
            }
            cards.forEach(card => {
                try {
                    const typeImage = src(card.querySelector(".stepBall_img_wrap .tw img"));
                    if (/u_text/i.test(typeImage)) { skipped++; return; }
                    const type = /\/stepball\/full\/([a-z]+)_text\.png/i.exec(typeImage);
                    if (type === null) throw new Error("No chart type in " + typeImage);
                    const digits = Array.from(card.querySelectorAll(".stepBall_img_wrap .numw img"))
                        .map(img => /_num_(\d)\.png/i.exec(src(img))).filter(match => match !== null).map(match => match[1]).join("");
                    const score = text(card, classic ? ".etc_con span.num" : ".li_in.ac i.tx").replaceAll(",", "");
                    const plate = /\/plate\/([a-z]+)\.png/i.exec(src(card.querySelector("img[src*='/plate/']")));
                    const broken = plate === null;
                    const gradeSlot = card.querySelector(".li_in.ac > img");
                    if (broken && ((gradeSlot !== null && src(gradeSlot) === "") || Number(score) === 0)) { skipped++; return; }
                    const grade = /\/grade\/([a-z_]+)\.png/i.exec(src(card.querySelector("img[src*='/grade/']")));
                    rows += [cell(text(card, ".song_name p")), type[1].toUpperCase() + (digits.length > 0 ? digits : "29"), score,
                        grade === null ? "" : grade[1].replace("_p", "+"), broken ? "" : plate[1], broken].join(",") + "\r\n";
                    scoreCount++;
                } catch (e) {
                    failedRows++;
                    console.error("Skipped a row on page " + page, e);
                }
            });
            console.log("Page " + page + " done");
            pageCount++;
            if (page >= maxPage || (lastPage !== null && page >= lastPage)) break;
        }
        if (scoreCount === 0) {
            alert("No scores to save - " + skipped + " stage breaks and UCS charts were skipped.");
            return;
        }
        const blobUrl = URL.createObjectURL(new Blob(["\uFEFF" + rows], { type: "text/csv;charset=utf-8" }));
        const link = document.createElement("a");
        link.href = blobUrl;
        link.download = "piu-scores.csv";
        document.body.appendChild(link);
        link.click();
        link.remove();
        URL.revokeObjectURL(blobUrl);
        console.log("Done - " + scoreCount + " scores across " + pageCount + " pages" + (skipped > 0 ? ", " + skipped + " stage breaks and UCS charts skipped" : "") + (failedRows > 0 ? ", " + failedRows + " rows skipped (see errors above)" : "") + ". Saved as piu-scores.csv");
        })();
        """;
    }
}
