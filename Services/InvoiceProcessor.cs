using blobprocessor.Models;

namespace blobprocessor.Services;

public sealed class InvoiceProcessor
{
    public async Task<InvoiceProcessingResult> ProcessAsync(
        Stream blob,
        string blobName,
        CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(blob);

        var content = await reader.ReadToEndAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(content))
        {
            return new InvoiceProcessingResult
            {
                BlobName = blobName,
                IsValid = false,
                FailureReason = "Blob is empty.",
                ContentLength = content.Length,
                ProcessedAtUtc = DateTime.UtcNow
            };
        }

        return new InvoiceProcessingResult
        {
            BlobName = blobName,
            IsValid = true,
            ContentLength = content.Length,
            ProcessedAtUtc = DateTime.UtcNow
        };
    }
}
