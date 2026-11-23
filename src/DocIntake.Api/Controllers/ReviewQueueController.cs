using DocIntake.Core;
using Microsoft.AspNetCore.Mvc;

namespace DocIntake.Api.Controllers;

/// <summary>Documents waiting for a human, with the reasons each one was flagged.</summary>
[ApiController]
[Route("review-queue")]
public class ReviewQueueController(IDocumentStore store) : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<Document>> Get(CancellationToken cancellationToken) =>
        await store.ListByStatusAsync(DocumentStatus.NeedsReview, cancellationToken);
}
