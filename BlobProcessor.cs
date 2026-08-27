using blobprocessor.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace blobprocessor;

public class BlobProcessor
{
    private readonly ILogger<BlobProcessor> _logger;
    private readonly InvoiceProcessor _invoiceProcessor;

    public BlobProcessor(
        ILogger<BlobProcessor> logger,
        InvoiceProcessor invoiceProcessor)
    {
        _logger = logger;
        _invoiceProcessor = invoiceProcessor;
    }

    [Function("BlobProcessor")]
public async Task Run(
    [BlobTrigger(
        "invoices/{name}",
        Source = BlobTriggerSource.EventGrid,
        Connection = "AzureWebJobsStorage")]
    Stream blob,
    string name,
    CancellationToken cancellationToken)
{
    using var scope = _logger.BeginScope(new Dictionary<string, object>
    {
        ["BlobName"] = name
    });

    _logger.LogInformation(
    "Blob processing started. BlobName: {BlobName}, SizeBytes: {SizeBytes}",
    name,
    blob.Length);

    var result = await _invoiceProcessor.ProcessAsync(
        blob,
        name,
        cancellationToken);

    if (!result.IsValid)
{
    _logger.LogWarning(
        "Invoice processing did not complete. BlobName: {BlobName}, ProcessingStatus: {ProcessingStatus}, FailureReason: {FailureReason}",
        result.BlobName,
        result.ProcessingStatus,
        result.FailureReason);

    return;
}

    _logger.LogInformation(
    "Invoice processing succeeded. BlobName: {BlobName}, ProcessingStatus: {ProcessingStatus}, ContentLength: {ContentLength}",
    result.BlobName,
    result.ProcessingStatus,
    result.ContentLength);

     }
}
