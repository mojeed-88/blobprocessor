using blobprocessor.Models;

namespace blobprocessor.Services;

public sealed class InvoiceProcessor
{
    private readonly ProcessedInvoiceStore _processedInvoiceStore;

    public InvoiceProcessor(ProcessedInvoiceStore processedInvoiceStore)
    {
        _processedInvoiceStore = processedInvoiceStore;
    }

    public async Task<InvoiceProcessingResult> ProcessAsync(
    Stream blob,
    string blobName,
    CancellationToken cancellationToken = default)
{
    var claimResult = await _processedInvoiceStore.TryClaimAsync(
    blobName,
    cancellationToken);

if (claimResult == InvoiceClaimResult.AlreadySucceeded)
{
    return new InvoiceProcessingResult
    {
        BlobName = blobName,
        ProcessingStatus = "Duplicate",
        IsValid = false,
        FailureReason = "Invoice has already been processed successfully.",
        ContentLength = 0,
        ProcessedAtUtc = DateTime.UtcNow
    };
}

if (claimResult == InvoiceClaimResult.InProgress)
{
    throw new InvalidOperationException(
        $"Invoice '{blobName}' is currently being processed.");
}

    using var reader = new StreamReader(blob);

    var content = await reader.ReadToEndAsync(cancellationToken);


    if (string.IsNullOrWhiteSpace(content))
    {
        await _processedInvoiceStore.MarkFailedAsync(
            blobName,
            "Blob is empty.",
            cancellationToken);

        return new InvoiceProcessingResult
        {
            BlobName = blobName,
	    ProcessingStatus = "ValidationFailed",
            IsValid = false,
            FailureReason = "Blob is empty.",
            ContentLength = content.Length,
            ProcessedAtUtc = DateTime.UtcNow
        };
    }

    await _processedInvoiceStore.MarkSucceededAsync(
        blobName,
        cancellationToken);

    return new InvoiceProcessingResult
    {
        BlobName = blobName,
	ProcessingStatus = "Succeeded",
        IsValid = true,
        ContentLength = content.Length,
        ProcessedAtUtc = DateTime.UtcNow
        };
    }
   
}
