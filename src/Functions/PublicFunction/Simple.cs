using System;
using System.Net.Http;
using System.Net.Http.Headers;
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

            // ----------------------------
            // ENVIRONMENT VARIABLES
            // ----------------------------
            string apimUrl = Environment.GetEnvironmentVariable("ApimUrl");
            string apimKey = Environment.GetEnvironmentVariable("ApimKey");
            string clientId = Environment.GetEnvironmentVariable("ClientId");
            string targetAppUri = Environment.GetEnvironmentVariable("TargetAppUri");
            string funcUrl = Environment.GetEnvironmentVariable("FunctionBaseURL");
            

            // ----------------------------
            // TEST MODE (NO APIM CONFIG)
            // ----------------------------
            bool apimConfigured =
                !string.IsNullOrWhiteSpace(apimUrl) &&
                !string.IsNullOrWhiteSpace(apimKey) &&
                !string.IsNullOrWhiteSpace(targetAppUri);

            if (!apimConfigured)
            {
                log.LogWarning("APIM env variables missing. Running in TEST MODE.");

                var testResponse = req.CreateResponse(System.Net.HttpStatusCode.OK);
                await testResponse.WriteStringAsync("Simple function running in TEST MODE (APIM not configured).");

                return testResponse;
            }

            // ----------------------------
            // PRODUCTION MODE (APIM + MSI)
            // ----------------------------
            try
            {
                log.LogInformation("APIM configured. Attempting MSI token retrieval...");

                var credentialOptions = new DefaultAzureCredentialOptions();
                if (!string.IsNullOrWhiteSpace(clientId))
                    credentialOptions.ManagedIdentityClientId = clientId;

                var credential = new DefaultAzureCredential(credentialOptions);

                // Correct MSI scope MUST be the target app
                var token = await credential.GetTokenAsync(
                    new TokenRequestContext(new[] { $"{targetAppUri}/.default" })
                );

                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", token.Token);

                _httpClient.DefaultRequestHeaders.Remove("Ocp-Apim-Subscription-Key");
                _httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", apimKey);

                //Get the parameter 
                var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
                string mode = query["mode"] ?? "novalue";
                
                //string callUrl = $"{apimUrl}/trusted-simple/test";
                //string callUrl = "https://func-apim-mi-week2-dbdgcba2hrffdbcx.eastus-01.azurewebsites.net/api/group";
                string callUrl = $"{funcUrl}/api/group?mode={mode}";
                log.LogInformation($"Calling Function Group endpoint: {callUrl}");

                var result = await _httpClient.GetAsync(callUrl);
                var body = await result.Content.ReadAsStringAsync();

                var response = req.CreateResponse(result.StatusCode);
                await response.WriteStringAsync(body);

                return response;
            }
            catch (Exception ex)
            {
                log.LogError(ex, "APIM call failed.");

                var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"APIM error: {ex.Message}");

                return errorResponse;
            }
        }
    }
}
