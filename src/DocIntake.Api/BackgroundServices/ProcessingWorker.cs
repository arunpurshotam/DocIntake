using DocIntake.Core.Abstractions;
using DocIntake.Core.Services;

namespace DocIntake.Api.BackgroundServices;

/// <summary>
/// Takes document ids off the job queue one at a time and runs them through the
/// <see cref="DocumentProcessor"/> (which applies the retry policy and dead-lettering).
/// </summary>
public class ProcessingWorker(
    IJobQueue queue,
    IDocumentStore store,
    DocumentProcessor processor,
    ILogger<ProcessingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessNextAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break; // normal shutdown
            }
            catch (Exception ex)
            {
                // Keep the worker alive: one bad job must not stop every later job.
                logger.LogError(ex, "Unexpected error while processing a job; continuing with the next one.");
            }
        }
    }

    /// <summary>Waits for the next job and processes it. Public so tests can run exactly one job.</summary>
    public async Task ProcessNextAsync(CancellationToken cancellationToken)
    {
        var documentId = await queue.DequeueAsync(cancellationToken);

        var document = await store.GetAsync(documentId, cancellationToken);
        if (document is null)
        {
            logger.LogWarning("Job for unknown document {DocumentId} skipped.", documentId);
            return;
        }

        await processor.ProcessAsync(document, cancellationToken);
        logger.LogInformation("Document {DocumentId} ({FileName}) finished as {Status}.",
            document.Id, document.FileName, document.Status);
    }
}
