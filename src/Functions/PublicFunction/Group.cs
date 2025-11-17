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
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req)
        {
            _logger.LogInformation("Starting function call");

            var clientId     = Environment.GetEnvironmentVariable("ClientId");
            var targetAppUri = Environment.GetEnvironmentVariable("TargetAppUri");
            var tenantId     = Environment.GetEnvironmentVariable("TenantId");
            var apiKey       = Environment.GetEnvironmentVariable("ApimKey");
            var apiUrl       = $"{Environment.GetEnvironmentVariable("ApimUrl")}/trusted-group/test";

            _logger.LogInformation($"API Url: {apiUrl}");
            _logger.LogInformation($"App Uri: {targetAppUri}");

            string jwt = string.Empty;

            try
            {
                var options = new DefaultAzureCredentialOptions();

                if (!string.IsNullOrWhiteSpace(clientId))
                    options.ManagedIdentityClientId = clientId;

                var credential = new DefaultAzureCredential(options);

                // Request token for downstream API
                var token = await credential.GetTokenAsync(
                    new TokenRequestContext(new[] { $"{targetAppUri}/.default" })
                );

                jwt = token.Token;

                _logger.LogInformation("Got the JWT");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting token");
            }

            // Add headers
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", jwt);

            _httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", apiKey);

            // Call APIM
            var result  = await _httpClient.GetAsync(apiUrl);
            var content = await result.Content.ReadAsStringAsync();

            _logger.LogInformation("Completed the APIM call");

            var response = req.CreateResponse(
                result.IsSuccessStatusCode ?
                System.Net.HttpStatusCode.OK :
                System.Net.HttpStatusCode.BadRequest);

            if (result.IsSuccessStatusCode)
            {
                var obj = JsonSerializer.Deserialize<TestResponse>(content,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                await response.WriteAsJsonAsync(obj);
            }
            else
            {
                _logger.LogError($"Error making API call: {content}");
                await response.WriteStringAsync(content);
            }

            return response;
        }
    }
}
