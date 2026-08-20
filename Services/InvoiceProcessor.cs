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
    var claimed = await _processedInvoiceStore.TryClaimAsync(
        blobName,
        cancellationToken);

    if (!claimed)
    {
        return new InvoiceProcessingResult
        {
            BlobName = blobName,
            IsValid = false,
            FailureReason = "Invoice has already been claimed for processing.",
            ContentLength = 0,
            ProcessedAtUtc = DateTime.UtcNow
        };
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
        IsValid = true,
        ContentLength = content.Length,
        ProcessedAtUtc = DateTime.UtcNow
        };
    }
   
}
