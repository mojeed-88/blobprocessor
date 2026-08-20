using Azure;
using Azure.Data.Tables;
using Microsoft.Extensions.Configuration;

namespace blobprocessor.Services;

public sealed class ProcessedInvoiceStore
{
    private const string TableName = "ProcessedInvoices";
    private const string PartitionKey = "Invoice";

    private readonly TableClient _tableClient;

    public ProcessedInvoiceStore(IConfiguration configuration)
    {
        var connectionString =
            configuration["ProcessedInvoicesStorage"]
            ?? throw new InvalidOperationException(
                "ProcessedInvoicesStorage configuration is missing.");

        _tableClient = new TableClient(connectionString, TableName);
    }

    public async Task<bool> TryClaimAsync(
        string invoiceId,
        CancellationToken cancellationToken = default)
    {
        var entity = new TableEntity(PartitionKey, invoiceId)
        {
            ["Status"] = "Processing",
            ["ClaimedAtUtc"] = DateTime.UtcNow
        };

        try
        {
            await _tableClient.AddEntityAsync(
                entity,
                cancellationToken);

            return true;
        }
        catch (RequestFailedException ex) when (ex.Status == 409)
        {
            return false;
        }
    }

    public async Task MarkSucceededAsync(
        string invoiceId,
        CancellationToken cancellationToken = default)
    {
        var entity = new TableEntity(PartitionKey, invoiceId)
        {
            ["Status"] = "Succeeded",
            ["CompletedAtUtc"] = DateTime.UtcNow
        };

        await _tableClient.UpsertEntityAsync(
            entity,
            TableUpdateMode.Merge,
            cancellationToken);
    }

    public async Task MarkFailedAsync(
        string invoiceId,
        string failureReason,
        CancellationToken cancellationToken = default)
    {
        var entity = new TableEntity(PartitionKey, invoiceId)
        {
            ["Status"] = "Failed",
            ["FailureReason"] = failureReason,
            ["FailedAtUtc"] = DateTime.UtcNow
        };

        await _tableClient.UpsertEntityAsync(
            entity,
            TableUpdateMode.Merge,
            cancellationToken);
    }
}
