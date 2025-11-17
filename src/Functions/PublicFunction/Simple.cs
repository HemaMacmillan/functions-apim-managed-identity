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
    public class Simple
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger _logger;

        public Simple(IHttpClientFactory httpClientFactory, ILoggerFactory loggerFactory)
        {
            _httpClient = httpClientFactory.CreateClient();
            _logger = loggerFactory.CreateLogger<Simple>();
        }

        [Function("simple")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req,
            FunctionContext context)
        {
            var log = context.GetLogger("simple");
        
            // Check if APIM settings exist
            string apimUrl = Environment.GetEnvironmentVariable("ApimUrl");
            string apimKey = Environment.GetEnvironmentVariable("ApimKey");
            string clientId = Environment.GetEnvironmentVariable("ClientId");
        
            bool apimConfigured = 
                !string.IsNullOrWhiteSpace(apimUrl) &&
                !string.IsNullOrWhiteSpace(apimKey);
        
            if (!apimConfigured)
            {
                log.LogWarning("APIM settings missing. Running in TEST MODE.");
                var testResponse = req.CreateResponse(System.Net.HttpStatusCode.OK);
                testResponse.WriteString("Simple function running in test mode (APIM not configured).");
                return testResponse;
            }
        
            // -----------------------
            // PRODUCTION MODE (APIM)
            // -----------------------
            try
            {
                var options = new DefaultAzureCredentialOptions();
        
                if (!string.IsNullOrWhiteSpace(clientId))
                    options.ManagedIdentityClientId = clientId;
        
                var credential = new DefaultAzureCredential(options);
        
                var token = await credential.GetTokenAsync(
                    new TokenRequestContext(new[] { "https://management.azure.com/.default" }));
        
                var http = new HttpClient();
                http.DefaultRequestHeaders.Authorization = 
                    new AuthenticationHeaderValue("Bearer", token.Token);
                http.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", apimKey);
        
                var fullUrl = $"{apimUrl}/trusted-simple/test";
        
                log.LogInformation($"Calling APIM URL: {fullUrl}");
        
                var result = await http.GetAsync(fullUrl);
                var content = await result.Content.ReadAsStringAsync();
        
                var response = req.CreateResponse(result.StatusCode);
                await response.WriteStringAsync(content);
                return response;
            }
            catch (Exception ex)
            {
                log.LogError(ex, "APIM call failed. Returning fallback.");
                var error = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
                error.WriteString($"Error calling APIM: {ex.Message}");
                return error;
            }
        }
    }
}
