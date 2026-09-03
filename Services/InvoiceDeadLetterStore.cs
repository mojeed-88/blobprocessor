using Azure.Data.Tables;
using Azure.Identity;
using Microsoft.Extensions.Configuration;

namespace blobprocessor.Services;

public sealed class InvoiceDeadLetterStore
{
    private const string TableName = "InvoiceDeadLetters";
    private const string PartitionKey = "DeadLetter";

    private readonly TableClient _tableClient;

    public InvoiceDeadLetterStore(IConfiguration configuration)
    {
        var localConnectionString =
            configuration["BusinessStorage"];

        if (!string.IsNullOrWhiteSpace(localConnectionString))
        {
            _tableClient = new TableClient(
                localConnectionString,
                TableName);

            return;
        }

        var tableEndpoint =
            configuration["BusinessTableEndpoint"]
            ?? throw new InvalidOperationException(
                "BusinessTableEndpoint configuration is missing.");
        _tableClient = new TableClient(
            new Uri(tableEndpoint),
            TableName,
            new DefaultAzureCredential());
    }

    public async Task SaveAsync(
        string blobName,
        string? containerName,
        string? functionId,
        string? blobType,
        string? eTag,
        CancellationToken cancellationToken = default)
    {
        var entity = new TableEntity(
            PartitionKey,
            Guid.NewGuid().ToString())
        {
            ["BlobName"] = blobName,
            ["ContainerName"] = containerName ?? string.Empty,
            ["FunctionId"] = functionId ?? string.Empty,
            ["BlobType"] = blobType ?? string.Empty,
            ["ETag"] = eTag ?? string.Empty,
            ["Status"] = "PendingReview",
            ["FailedAtUtc"] = DateTime.UtcNow
        };

        await _tableClient.AddEntityAsync(
            entity,
            cancellationToken);
    }
}
