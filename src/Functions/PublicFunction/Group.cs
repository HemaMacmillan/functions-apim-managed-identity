using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace PublicFunction
{
    public class Group
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger _logger;

        public Group(IHttpClientFactory httpClientFactory, ILoggerFactory loggerFactory)
        {
            _httpClient = httpClientFactory.CreateClient();
            _logger = loggerFactory.CreateLogger<Group>();
        }

        [Function("group")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req,
            FunctionContext context)
        {
            var log = context.GetLogger("group");
            log.LogInformation("Starting function call");

            // ----------------------------
            // ENVIRONMENT VARIABLES
            // ----------------------------
            var clientId     = Environment.GetEnvironmentVariable("ClientId");
            var targetAppUri = Environment.GetEnvironmentVariable("TargetAppUri");
            var tenantId     = Environment.GetEnvironmentVariable("TenantId");
            var apiKey       = Environment.GetEnvironmentVariable("ApimKey");
            var apimUrl      = Environment.GetEnvironmentVariable("ApimUrl");

            // ----------------------------
            // TEST MODE (NO APIM SETTINGS)
            // ----------------------------
            bool apimConfigured =
                !string.IsNullOrWhiteSpace(apiKey) &&
                !string.IsNullOrWhiteSpace(apimUrl) &&
                !string.IsNullOrWhiteSpace(targetAppUri);

            if (!apimConfigured)
            {
                log.LogWarning("APIM environment settings missing. Running in TEST MODE.");

                var testResponse = req.CreateResponse(System.Net.HttpStatusCode.OK);
                await testResponse.WriteStringAsync("Group function running in TEST MODE (APIM not configured).");

                return testResponse;
            }

            // ----------------------------
            // PRODUCTION MODE (MSI + APIM)
            // ----------------------------
            string jwt = string.Empty;

            try
            {
                log.LogInformation("APIM configured. Attempting MSI token retrieval...");

                var options = new DefaultAzureCredentialOptions();
                if (!string.IsNullOrWhiteSpace(clientId))
                    options.ManagedIdentityClientId = clientId;

                var credential = new DefaultAzureCredential(options);

                var token = await credential.GetTokenAsync(
                    new TokenRequestContext(new[] { $"{targetAppUri}/.default" })
                );

                jwt = token.Token;

                log.LogInformation("MSI token successfully retrieved.");
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Error retrieving MSI token.");
            }

            // ----------------------------
            // CALL APIM
            // ----------------------------
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", jwt);

            _httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", apiKey);

            string fullUrl = $"{apimUrl}/trusted-group/test";
            log.LogInformation($"Calling APIM endpoint: {fullUrl}");

            var result = await _httpClient.GetAsync(fullUrl);
            var content = await result.Content.ReadAsStringAsync();

            log.LogInformation("Completed APIM call.");

            var response = req.CreateResponse(result.IsSuccessStatusCode
                ? System.Net.HttpStatusCode.OK
                : System.Net.HttpStatusCode.BadRequest);

            if (result.IsSuccessStatusCode)
            {
                var obj = JsonSerializer.Deserialize<TestResponse>(
                    content,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                await response.WriteAsJsonAsync(obj);
            }
            else
            {
                log.LogError($"APIM call error: {content}");
                await response.WriteStringAsync(content);
            }

            return response;
        }
    }
}
