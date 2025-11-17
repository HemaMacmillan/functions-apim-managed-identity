using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace PublicFunction
{
    public class Test
    {
        private readonly ILogger _logger;

        public Test(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<Test>();
        }

        [Function("test")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req,
            FunctionContext context)
        {
            _logger.LogInformation("Starting function call");

            // Claims come from FunctionContext in isolated model
            var identity = context.GetHttpContext()?.User;

            // BUT: Isolated worker does NOT have HttpContext by default.
            // Claims are found in: context.Authentication
            var claimsPrincipal = context.Features.Get<JwtPrincipalFeature>()?.Principal;

            if (claimsPrincipal == null)
            {
                _logger.LogWarning("No claims found for this request.");
            }

            var claims = claimsPrincipal?.Claims?.ToList() ?? Enumerable.Empty<System.Security.Claims.Claim>().ToList();

            foreach (var c in claims)
                _logger.LogInformation($"{c.Type} - {c.Value} - {c.ValueType}");

            var roleClaim = claims.FirstOrDefault(c => c.Type == "roles");

            var response = req.CreateResponse(System.Net.HttpStatusCode.OK);

            await response.WriteAsJsonAsync(new TestResponse
            {
                DateOfMessage = DateTime.Now,
                Message = roleClaim == null
                    ? "Hello from the Private Function! No role claim found."
                    : $"Hello from the Private Function! The APIM Managed Identity has been assigned to the role: {roleClaim.Value}"
            });

            return response;
        }
    }

    public class TestResponse
    {
        public string Message { get; set; }
        public DateTime DateOfMessage { get; set; }
    }
}
