using DocIntake.Core.Abstractions;
using DocIntake.Core.Domain;
using DocIntake.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace DocIntake.Api.Controllers;

[ApiController]
[Route("documents")]
public class DocumentsController(IDocumentStore store, DocumentProcessor processor) : ControllerBase
{
    /// <summary>Accepts an uploaded file, registers it, and processes it right away.</summary>
    [HttpPost]
    [ProducesResponseType<UploadResponse>(StatusCodes.Status201Created)]
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

        var document = new Document(fileName);

        // Save first so the document exists as Received even if processing hits an unexpected error.
        await store.SaveAsync(document, cancellationToken);

        // Synchronous for now; a background worker will take this over in a later milestone.
        await using (var content = file.OpenReadStream())
        {
            await processor.ProcessAsync(document, content, cancellationToken);
        }

        return CreatedAtAction(nameof(GetById), new { id = document.Id }, new UploadResponse(document.Id, document.Status));
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

    /// <summary>What POST /documents returns: the new document's Id and its status after processing.</summary>
    public record UploadResponse(Guid Id, DocumentStatus Status);
}
