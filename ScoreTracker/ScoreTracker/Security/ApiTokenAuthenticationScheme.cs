using System.Text;
using System.Text.Encodings.Web;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using ScoreTracker.Application.Queries;
using ScoreTracker.Web.Accessors;

namespace ScoreTracker.Web.Security
{
    public class ApiTokenAuthenticationScheme : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ApiTokenAuthenticationScheme(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger,
            UrlEncoder encoder, ISystemClock clock,
            IHttpContextAccessor httpContextAccessor) : base(options, logger, encoder, clock)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var context = _httpContextAccessor.HttpContext;
            if (context == null) return AuthenticateResult.Fail("Not in an HTTP Context");

            if (!context.Request.Headers.TryGetValue("Authorization", out var authHeaders))
                return AuthenticateResult.Fail("Authorization Header is missing");

            if (authHeaders.Count() != 1) return AuthenticateResult.Fail("Only one Authorization header is allowed");
            var authHeader = authHeaders[0];
            if (string.IsNullOrWhiteSpace(authHeader)) return AuthenticateResult.Fail("Authorization header is empty");

            if (!authHeader.StartsWith("Basic ")) return AuthenticateResult.Fail("Authorization header must be Basic");

            var encodedToken = authHeader["Basic ".Length..].Trim();
            if (string.IsNullOrWhiteSpace(encodedToken)) return AuthenticateResult.Fail("Encoded token is missing");
            var encoding = Encoding.GetEncoding("iso-8859-1");
            string usernamePassword;
            try
            {
                usernamePassword = encoding.GetString(Convert.FromBase64String(encodedToken));
            }
            catch (Exception e)
            {
                return AuthenticateResult.Fail("Could not decode token");
            }

            var split = usernamePassword.Split(":");
            if (split.Length != 2)
                return AuthenticateResult.Fail(@"Encoded token must be formatted as "":<ApiToken>""");

            if (!Guid.TryParse(split[1], out var apiToken))
                return AuthenticateResult.Fail("Decrypted API Token is invalid (should be a GUID)");

            var user = await context.RequestServices.GetRequiredService<IMediator>()
                .Send(new GetUserByApiTokenQuery(apiToken));

            if (user == null) return AuthenticateResult.Fail("User with this api token was not found");

            return AuthenticateResult.Success(new AuthenticationTicket(user.GetClaimsPrincipal(), "ApiToken"));
        }
    }
}
