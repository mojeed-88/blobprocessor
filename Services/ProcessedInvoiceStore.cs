using Azure;
using Azure.Data.Tables;
using Azure.Identity;
using Microsoft.Extensions.Configuration;

namespace blobprocessor.Services;

public sealed class ProcessedInvoiceStore
{
    private const string TableName = "ProcessedInvoices";
    private const string PartitionKey = "Invoice";

    private readonly TableClient _tableClient;

    public ProcessedInvoiceStore(IConfiguration configuration)
    {
        var localConnectionString =
            configuration["ProcessedInvoicesStorage"];

        if (!string.IsNullOrWhiteSpace(localConnectionString))
        {
            // Local development with Azurite
            _tableClient = new TableClient(
                localConnectionString,
                TableName);

            return;
        }

        var tableEndpoint =
            configuration["ProcessedInvoicesTableEndpoint"]
            ?? throw new InvalidOperationException(
                "ProcessedInvoicesTableEndpoint configuration is missing.");

        _tableClient = new TableClient(
            new Uri(tableEndpoint),
            TableName,
            new DefaultAzureCredential());
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
