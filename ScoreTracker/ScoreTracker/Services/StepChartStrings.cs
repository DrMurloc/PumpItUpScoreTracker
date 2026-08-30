using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace ScoreTracker.Web.Services;

/// <summary>
///     The step-chart renderer's localized labels, serialized into the JSON block the module
///     reads (docs/design/step-chart-failure-map.md D12). One builder for both hosts — the
///     chart page's static section and the dialog's Steps tab — so the copy cannot drift
///     between them. Keys resolve through the localizer like everything else; before the i18n
///     pass lands they render their English key text, the convention's own fallback.
/// </summary>
public static class StepChartStrings
{
    public static MarkupString For<T>(IStringLocalizer<T> localizer)
    {
        return new MarkupString(JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["crux"] = localizer["Crux"],
            ["range"] = localizer["Notable run"],
            ["upper"] = localizer["Upper"],
            ["lower"] = localizer["Lower"],
            ["center"] = localizer["Center"],
            ["deathSpike"] = localizer["Death spike"],
            ["passCluster"] = localizer["Pass cluster"],
            ["lifeBreak"] = localizer["Life Bar Break"],
            ["passG"] = localizer["Pass G"],
            ["walkOff"] = localizer["Walk off"],
            ["unknownBreak"] = localizer["Unknown Break"],
            ["stagePass"] = localizer["Stage Pass"],
            ["yourRuns"] = localizer["Your runs"],
            ["unplaced"] = localizer["Unplaced"],
            ["scrollSpeed"] = localizer["Scroll speed"],
            ["finishedOne"] = localizer["1 broken run made it to the end"],
            ["finishedMany"] = localizer["{0} broken runs made it to the end"],
            ["leftFoot"] = localizer["Left foot"],
            ["rightFoot"] = localizer["Right foot"],
            ["quarters"] = localizer["Quarters"],
            ["eighths"] = localizer["Eighths"],
            ["twelfths"] = localizer["Twelfths"],
            ["sixteenths"] = localizer["Sixteenths"],
            ["finer"] = localizer["Finer"],
            ["timingUnavailable"] = localizer["Timing colors need a step file that aligned to beats."]
        }));
    }
}
