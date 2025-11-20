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
            [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req,
            FunctionContext context)
        {
            var log = context.GetLogger("group");
        
            // Extract query parameter
            var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
            string mode = query["mode"]?.ToLower();
        
            var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
        
            switch (mode)
            {
                case "static":
                    await response.WriteStringAsync("Group API → static response.");
                    break;
        
                case "storage":
                    await response.WriteStringAsync("Group API → storage logic placeholder.");
                    break;
        
                case "keyvault":
                    await response.WriteStringAsync("Group API → keyvault logic placeholder.");
                    break;
        
                default:
                    await response.WriteStringAsync("Group API → unknown mode. Use ?mode=static|storage|keyvault.");
                    break;
            }
        
            return response;
        }

    }
}
