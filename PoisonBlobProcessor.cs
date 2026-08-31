using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace blobprocessor;

public sealed class PoisonBlobProcessor
{
    private readonly ILogger<PoisonBlobProcessor> _logger;

    public PoisonBlobProcessor(
        ILogger<PoisonBlobProcessor> logger)
    {
        _logger = logger;
    }

    [Function("PoisonBlobProcessor")]
    public void Run(
        [QueueTrigger(
            "webjobs-blobtrigger-poison",
            Connection = "AzureWebJobsStorage")]
        string message)
    {
        var poisonMessage =
            JsonSerializer.Deserialize<PoisonBlobMessage>(message);

        if (poisonMessage is null)
        {
            _logger.LogError(
                "Poison blob message could not be deserialized.");

            return;
        }

        _logger.LogError(
            "Blob processing exhausted all retries. FunctionId: {FunctionId}, ContainerName: {ContainerName}, BlobName: {BlobName}, BlobType: {BlobType}, ETag: {ETag}",
            poisonMessage.FunctionId,
            poisonMessage.ContainerName,
            poisonMessage.BlobName,
            poisonMessage.BlobType,
            poisonMessage.ETag);
    }
}

public sealed class PoisonBlobMessage
{
    public string? Type { get; set; }

    public string? FunctionId { get; set; }

    public string? BlobType { get; set; }

    public string? ContainerName { get; set; }

    public string? BlobName { get; set; }

    public string? ETag { get; set; }
}
