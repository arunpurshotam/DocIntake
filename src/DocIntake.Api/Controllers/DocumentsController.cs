using DocIntake.Core;
using Microsoft.AspNetCore.Mvc;

namespace DocIntake.Api.Controllers;

[ApiController]
[Route("documents")]
public class DocumentsController(IDocumentStore store) : ControllerBase
{
    /// <summary>Accepts an uploaded file and registers it as a new document.</summary>
    [HttpPost]
    public async Task<IActionResult> Upload(IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest("A non-empty file is required.");
        }

        // Milestone 1 records metadata only; file contents are not stored yet.
        var document = new Document(Path.GetFileName(file.FileName));
        await store.SaveAsync(document, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = document.Id }, new { id = document.Id });
    }

    /// <summary>Returns a document by Id, or 404 if it does not exist.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Document>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var document = await store.GetAsync(id, cancellationToken);
        return document is null ? NotFound() : document;
    }
}
