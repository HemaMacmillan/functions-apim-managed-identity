using System;
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
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req)
        {
            _logger.LogInformation("Test function executed");

            var response = req.CreateResponse(System.Net.HttpStatusCode.OK);

            await response.WriteAsJsonAsync(new TestResponse
            {
                DateOfMessage = DateTime.Now,
                Message = "Hello from the Private Function!"
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
