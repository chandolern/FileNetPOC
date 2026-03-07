Ah, I understand completely now! That clarification is huge. You are describing a federated Identity architecture.

Using an enterprise Customer Identity and Access Management (CIAM) platform—like **Azure AD B2C** or **Auth0**—is exactly the right move. They natively handle the Google/Microsoft logins, manage the sessions, and allow us to inject custom roles or permissions into the JWT before it reaches your API. We can absolutely integrate that seamlessly once the core system is running.

For now, let's cross the finish line on our FileNet vertical slice. We just need to expose your MediatR pipeline to the outside world.

Let's build the **`DocumentsController`** in your `Api` project.

### Step 1: Create the Controller

Open your terminal and navigate to the `Controllers` folder in your API project (create it if it doesn't exist):

```bash
cd ~/Dev/FileNetPOC/src/FileNetPOC.Api
mkdir -p Controllers
touch Controllers/DocumentsController.cs

```

### Step 2: Implement the API Endpoint

Open `Controllers/DocumentsController.cs` and add this code.

Notice how thin this controller is. Its *only* job is to take the HTTP-specific `IFormFile`, convert it into pure C# bytes, and hand it off to MediatR. It doesn't know anything about Cosmos DB or Azure Storage.

```csharp
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

```

### Step 3: Test the Complete Vertical Slice!

Your infrastructure is running in Azure, your configuration is wired up, your MediatR pipeline is strict, and your endpoint is ready. Let's fire it up.

Run this from the root of your project:

```bash
cd ~/Dev/FileNetPOC
dotnet run --project src/FileNetPOC.Api/FileNetPOC.Api.csproj

```

Once the application starts, open your browser and navigate to the Swagger UI (usually `http://localhost:5000/swagger` or `https://localhost:5001/swagger` depending on your console output).

Try uploading a small file using the Swagger interface!