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
        // api/v2's catalog reads serialize themselves so the ETag can hash the body in one pass, so
        // they arrive as a ContentResult carrying finished JSON rather than an object to serialize.
        var actual = result switch
        {
            ContentResult c => Reindent(c.Content ?? string.Empty),
            JsonResult j => JsonSerializer.Serialize(j.Value, Wire),
            ObjectResult o => JsonSerializer.Serialize(o.Value, Wire),
            _ => throw new InvalidOperationException($"Unexpected action result type {result.GetType().Name}")
        };
        Assert.Equal(Normalize(expectedJson), Normalize(actual));
    }

    private static string Normalize(string json)
    {
        return json.Replace("\r\n", "\n").Trim();
    }

    /// <summary>
    ///     Round-trips compact JSON through the same indented options the goldens are written in, so a
    ///     self-serialized body is compared on content rather than on whitespace.
    /// </summary>
    private static string Reindent(string json)
    {
        return JsonSerializer.Serialize(JsonSerializer.Deserialize<JsonElement>(json), Wire);
    }
}
