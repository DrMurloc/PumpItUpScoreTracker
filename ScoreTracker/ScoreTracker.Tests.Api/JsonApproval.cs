using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace ScoreTracker.Tests.Api;

internal static class JsonApproval
{
    // MVC serializes API responses with the framework's web defaults (camelCase property names,
    // default encoder — note it escapes '+' as + on the wire); Program.cs does not customize
    // MVC's JsonOptions, so these options pin the actual wire shape partner tools receive.
    // If an assertion here breaks, a public API contract changed.
    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static void AssertWireShape(string expectedJson, IActionResult result)
    {
        var payload = result switch
        {
            JsonResult j => j.Value,
            ObjectResult o => o.Value,
            _ => throw new InvalidOperationException($"Unexpected action result type {result.GetType().Name}")
        };
        var actual = JsonSerializer.Serialize(payload, Wire);
        Assert.Equal(Normalize(expectedJson), Normalize(actual));
    }

    private static string Normalize(string json)
    {
        return json.Replace("\r\n", "\n").Trim();
    }
}
