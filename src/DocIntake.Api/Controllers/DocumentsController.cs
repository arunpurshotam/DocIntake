using System.Security.Cryptography;
using DocIntake.Core.Abstractions;
using DocIntake.Core.Domain;
using Microsoft.AspNetCore.Mvc;

namespace DocIntake.Api.Controllers;

[ApiController]
[Route("documents")]
public class DocumentsController(
    IDocumentStore store,
    IDocumentContentStore contentStore,
    IJobQueue queue) : ControllerBase
{
    /// <summary>
    /// Accepts an uploaded file and queues it for background processing (202 Accepted).
    /// Uploading the same bytes again returns the existing document instead (200 OK).
    /// </summary>
    [HttpPost]
    [ProducesResponseType<UploadResponse>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<UploadResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Upload(IFormFile? file, CancellationToken cancellationToken)
    {
        // Distinguish "nothing attached" from "attached but empty" so the error says what actually went wrong.
        if (file is null)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "No file attached.",
                detail: DescribeMissingFile());
        }

        var fileName = Path.GetFileName(file.FileName);
        if (file.Length == 0)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "The uploaded file is empty.",
                detail: $"'{fileName}' was received but contains 0 bytes. Upload a file with content.");
        }

        byte[] content;
        using (var buffer = new MemoryStream())
        {
            await file.CopyToAsync(buffer, cancellationToken);
            content = buffer.ToArray();
        }
        var hash = Convert.ToHexStringLower(SHA256.HashData(content));

        // Idempotent: the same bytes always map to the same document, and are only queued once.
        var existing = await store.FindByContentHashAsync(hash, cancellationToken);
        if (existing is not null)
        {
            return Ok(new UploadResponse(existing.Id, existing.Status));
        }

        var document = new Document(fileName, hash);

        // Content first, then the Received document, then the job: by the time the worker
        // sees the id, everything it needs is already stored.
        await contentStore.SaveAsync(document.Id, content, cancellationToken);
        await store.SaveAsync(document, cancellationToken);
        await queue.EnqueueAsync(document.Id, cancellationToken);

        return AcceptedAtAction(nameof(GetById), new { id = document.Id }, new UploadResponse(document.Id, document.Status));
    }

    /// <summary>Returns a document by Id, or 404 if it does not exist.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<Document>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Document>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var document = await store.GetAsync(id, cancellationToken);
        return document is null ? NotFound() : document;
    }

    // If files arrived under a different form field name, say so; that is the usual reason 'file' is missing.
    private string DescribeMissingFile()
    {
        const string expected = "Send the file in a multipart form field named 'file', e.g. curl -F \"file=@path/to/file\".";

        var receivedFields = Request?.HasFormContentType == true
            ? Request.Form.Files.Select(f => f.Name).Distinct().ToList()
            : [];

        return receivedFields.Count == 0
            ? expected
            : $"{expected} Received file field(s): {string.Join(", ", receivedFields.Select(n => $"'{n}'"))}.";
    }

    /// <summary>What POST /documents returns: the document's Id and its current status.</summary>
    public record UploadResponse(Guid Id, DocumentStatus Status);
}
