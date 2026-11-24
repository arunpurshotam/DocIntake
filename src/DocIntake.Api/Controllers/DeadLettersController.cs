using DocIntake.Core.Abstractions;
using DocIntake.Core.Domain;
using Microsoft.AspNetCore.Mvc;

namespace DocIntake.Api.Controllers;

/// <summary>Documents that failed processing for good, with the error and how many attempts were made.</summary>
[ApiController]
[Route("dead-letters")]
public class DeadLettersController(IDeadLetterStore deadLetters) : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<DeadLetter>> Get(CancellationToken cancellationToken) =>
        await deadLetters.ListAsync(cancellationToken);
}
