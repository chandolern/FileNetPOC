using MediatR;
using Microsoft.AspNetCore.Mvc;
using FileNetPOC.Services.Features.Documents.Commands.UploadDocument;

namespace FileNetPOC.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DocumentsController : ControllerBase
{
    private readonly IMediator _mediator;

    public DocumentsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("upload")]
    public async Task<IActionResult> UploadDocument([FromForm] IFormFile file, [FromForm] string author)
    {
        // 1. Quick sanity check before allocating memory
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { detail = "No file was provided." });
        }

        // 2. Convert the HTTP IFormFile into a pure C# byte array for our Service layer
        using var memoryStream = new MemoryStream();
        await file.CopyToAsync(memoryStream);
        var fileBytes = memoryStream.ToArray();

        // 3. Construct the Command
        var command = new UploadDocumentCommand(
            FileName: file.FileName,
            ContentType: file.ContentType,
            FileSize: file.Length,
            FileContent: fileBytes,
            Author: author
        );

        // 4. Dispatch the command through our pipeline (Logging -> Validation -> Handler)
        var documentId = await _mediator.Send(command);

        // 5. Return success to the client
        return Ok(new { 
            DocumentId = documentId, 
            Message = "Document uploaded to Azure successfully." 
        });
    }
}