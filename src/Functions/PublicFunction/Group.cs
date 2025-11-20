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
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;

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
                {
                    var accountName = Environment.GetEnvironmentVariable("StorageAccountName");
                    var containerName = Environment.GetEnvironmentVariable("BlobContainerName");
                    var blobName = Environment.GetEnvironmentVariable("BlobName");
            
                    var uri = new Uri($"https://{accountName}.blob.core.windows.net/{containerName}/{blobName}");
            
                    var credential = new DefaultAzureCredential();
                    var client = new BlobClient(uri, credential);
            
                    var download = await client.DownloadContentAsync();
                    var text = download.Value.Content.ToString();
            
                    await response.WriteStringAsync($"Storage content: {text}");
                    break;
                }
                case "keyvault":
                {
                    string vaultUrl = Environment.GetEnvironmentVariable("KeyVaultUrl");   // e.g., https://kv-week2-hema.vault.azure.net/
                    string secretName = "demo-secret";
                
                    var client = new SecretClient(new Uri(vaultUrl), new DefaultAzureCredential());
                    KeyVaultSecret secret = await client.GetSecretAsync(secretName);
                
                    await response.WriteStringAsync($"Group API → KeyVault value: {secret.Value}");
                    break;
                }
                default:
                    await response.WriteStringAsync("Group API → unknown mode. Use ?mode=static|storage|keyvault.");
                    break;
            }
        
            return response;
        }

    }
}
