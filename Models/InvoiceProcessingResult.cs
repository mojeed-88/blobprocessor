namespace blobprocessor.Models;

public sealed class InvoiceProcessingResult
{
    public required string BlobName { get; init; }

    public required string ProcessingStatus { get; init; }

    public bool IsValid { get; init; }

    public string? FailureReason { get; init; }

    public int ContentLength { get; init; }

    public DateTime ProcessedAtUtc { get; init; }
}
