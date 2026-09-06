using Microsoft.AspNetCore.Mvc;

namespace ScoreTracker.Web.Controllers.Api.V2;

/// <summary>
///     Declares one RFC 9457 problem response for Swagger: a <see cref="ProblemDetails" /> body
///     under <c>application/problem+json</c>, which is what <see cref="ApiV2ControllerBase.Problem" />
///     actually writes. A derived attribute rather than the full declaration on every action, so
///     an action lists its statuses in one glance and the content type cannot drift per action.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
internal sealed class ProducesProblemAttribute : ProducesResponseTypeAttribute
{
    public ProducesProblemAttribute(int statusCode)
        : base(typeof(ProblemDetails), statusCode, "application/problem+json")
    {
    }
}
