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

    public async Task<InvoiceClaimResult> TryClaimAsync(
        string invoiceId,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var entity = new TableEntity(PartitionKey, invoiceId)
        {
            ["Status"] = "Processing",
            ["ClaimedAtUtc"] = now
        };

        try
        {
            await _tableClient.AddEntityAsync(
                entity,
                cancellationToken);

            return InvoiceClaimResult.Claimed;
        }
        catch (RequestFailedException ex) when (ex.Status == 409)
        {
            var existingResponse =
                await _tableClient.GetEntityAsync<TableEntity>(
                    PartitionKey,
                    invoiceId,
                    cancellationToken: cancellationToken);

            var existingEntity = existingResponse.Value;
            var status = existingEntity.GetString("Status");

            if (string.Equals(
                status,
                "Succeeded",
                StringComparison.OrdinalIgnoreCase))
            {
                return InvoiceClaimResult.AlreadySucceeded;
            }

            if (string.Equals(
                status,
                "Processing",
                StringComparison.OrdinalIgnoreCase))
            {
                var claimedAtUtc =
                    existingEntity.GetDateTimeOffset("ClaimedAtUtc");

                if (claimedAtUtc.HasValue &&
                    now - claimedAtUtc.Value.UtcDateTime <
                    TimeSpan.FromMinutes(5))
                {
                    return InvoiceClaimResult.InProgress;
                }
            }

            existingEntity["Status"] = "Processing";
            existingEntity["ClaimedAtUtc"] = now;

            try
            {
                await _tableClient.UpdateEntityAsync(
                    existingEntity,
                    existingEntity.ETag,
                    TableUpdateMode.Merge,
                    cancellationToken);

                return InvoiceClaimResult.Claimed;
            }
            catch (RequestFailedException updateEx)
                when (updateEx.Status == 412)
            {
                return InvoiceClaimResult.InProgress;
            }
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
            TableUpdateMode.Replace,
            cancellationToken);
    }

    public async Task MarkValidationFailedAsync(
        string invoiceId,
        string failureReason,
        CancellationToken cancellationToken = default)
    {
        var entity = new TableEntity(PartitionKey, invoiceId)
        {
            ["Status"] = "ValidationFailed",
            ["FailureReason"] = failureReason,
            ["FailedAtUtc"] = DateTime.UtcNow
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