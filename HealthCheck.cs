using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace blobprocessor;

public class HealthCheck
{
    private readonly ILogger<HealthCheck> _logger;

    public HealthCheck(ILogger<HealthCheck> logger)
    {
        _logger = logger;
    }

    [Function("HealthCheck")]
    public IActionResult Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequest req)
    {
        _logger.LogInformation(
    "Health check endpoint invoked. Method: {Method}, Path: {Path}, TimeUtc: {TimeUtc}",
    req.Method,
    req.Path,
    DateTime.UtcNow);
        return new OkObjectResult(new
{
    status = "Healthy",
    service = "blobprocessor",
    timestampUtc = DateTime.UtcNow
});
    }
}
