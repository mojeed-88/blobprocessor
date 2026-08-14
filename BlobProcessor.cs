using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace blobprocessor;

public class BlobProcessor
{
    private readonly ILogger<BlobProcessor> _logger;

    public BlobProcessor(ILogger<BlobProcessor> logger)
    {
        _logger = logger;
    }

    [Function("BlobProcessor")]
public async Task Run(
    [BlobTrigger("invoices/{name}", Connection = "AzureWebJobsStorage")]
    Stream blob,
    string name)
{
    _logger.LogInformation(
        "Blob processing started. BlobName: {BlobName}, SizeBytes: {SizeBytes}, TimeUtc: {TimeUtc}",
        name,
        blob.Length,
        DateTime.UtcNow);

    using var reader = new StreamReader(blob);
    var content = await reader.ReadToEndAsync();

    _logger.LogInformation(
        "Blob content read successfully. BlobName: {BlobName}, ContentLength: {ContentLength}",
        name,
        content.Length);

    if (string.IsNullOrWhiteSpace(content))
    {
        _logger.LogWarning(
            "Blob validation failed because the blob is empty. BlobName: {BlobName}",
            name);

        return;
    }

    _logger.LogInformation(
        "Blob validation succeeded. BlobName: {BlobName}",
        name);
	
    }
}
