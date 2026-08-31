using System.Text.Json;
using blobprocessor.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace blobprocessor;

public sealed class PoisonBlobProcessor
{
    private readonly ILogger<PoisonBlobProcessor> _logger;
    private readonly InvoiceDeadLetterStore _deadLetterStore;

    public PoisonBlobProcessor(
        ILogger<PoisonBlobProcessor> logger,
        InvoiceDeadLetterStore deadLetterStore)
    {
        _logger = logger;
        _deadLetterStore = deadLetterStore;
    }

    [Function("PoisonBlobProcessor")]
    public async Task Run(
        [QueueTrigger(
            "webjobs-blobtrigger-poison",
            Connection = "AzureWebJobsStorage")]
        string message,
        CancellationToken cancellationToken)
    {
        var poisonMessage =
            JsonSerializer.Deserialize<PoisonBlobMessage>(message);

        if (poisonMessage is null)
        {
            throw new InvalidOperationException(
                "Poison blob message could not be deserialized.");
        }

        if (string.IsNullOrWhiteSpace(poisonMessage.BlobName))
        {
            throw new InvalidOperationException(
                "Poison blob message does not contain a BlobName.");
        }

        await _deadLetterStore.SaveAsync(
            poisonMessage.BlobName,
            poisonMessage.ContainerName,
            poisonMessage.FunctionId,
            poisonMessage.BlobType,
            poisonMessage.ETag,
            cancellationToken);

        _logger.LogError(
            "Blob processing exhausted all retries and was persisted to the dead-letter store. FunctionId: {FunctionId}, ContainerName: {ContainerName}, BlobName: {BlobName}, BlobType: {BlobType}, ETag: {ETag}",
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
