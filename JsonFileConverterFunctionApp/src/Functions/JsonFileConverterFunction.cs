using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace JsonFileConverterFunctionApp.src.Functions
{
    public class JsonFileConverterFunction
    {
        private readonly ILogger<JsonFileConverterFunction> _logger;

        public JsonFileConverterFunction(ILogger<JsonFileConverterFunction> logger)
        {
            _logger = logger;
        }

        [Function("JsonFileConverterFunction")]
        public IActionResult Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequest req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");
            return new OkObjectResult("Welcome to Azure Functions!");
        }
    }
}
