You got it. Let's knock out the Validator and the Handler in one go.

Before we write the Handler, we need the actual database model to store the data. Let's quickly create the `DocumentMetadata` entity in your `Shared` project, and then we will write the Validator and Handler in your `Services` project.

### Step 1: Create the Document Metadata Entity

This is the object we will save to Cosmos DB. It inherits the `id` property from `BaseEntity`.

Open your terminal and create the file:

```bash
touch src/FileNetPOC.Shared/Entities/DocumentMetadata.cs

```

Add this code to `DocumentMetadata.cs`:

```csharp
namespace FileNetPOC.Shared.Entities;

public class DocumentMetadata : BaseEntity
{
    public string FileName { get; set; } = string.Empty;
    public string BlobUri { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string Author { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    
    // For this POC, we will use the Author as the logical partition key for Cosmos DB
    public string PartitionKey => Author; 
}

```

---

### Step 2: Create the Validator

This is the Gatekeeper we set up FluentValidation to look for. It ensures we don't upload empty files or corrupted data to Azure.

Create the file in your Command folder:

```bash
touch src/FileNetPOC.Services/Features/Documents/Commands/UploadDocument/UploadDocumentCommandValidator.cs

```

Add this code to `UploadDocumentCommandValidator.cs`:

```csharp
using FluentValidation;

namespace FileNetPOC.Services.Features.Documents.Commands.UploadDocument;

public class UploadDocumentCommandValidator : AbstractValidator<UploadDocumentCommand>
{
    public UploadDocumentCommandValidator()
    {
        RuleFor(x => x.FileName)
            .NotEmpty().WithMessage("File name is required.");
            
        RuleFor(x => x.ContentType)
            .NotEmpty().WithMessage("Content type is required.");
            
        RuleFor(x => x.Author)
            .NotEmpty().WithMessage("Author is required.");
            
        RuleFor(x => x.FileSize)
            .GreaterThan(0).WithMessage("File cannot be empty.")
            // Limiting to 25MB for the POC
            .LessThanOrEqualTo(25 * 1024 * 1024).WithMessage("File size must not exceed 25 MB."); 
            
        RuleFor(x => x.FileContent)
            .NotNull().NotEmpty().WithMessage("File content is required.");
    }
}

```

---

### Step 3: Create the Handler

This is the orchestrator. It receives the validated Command, uploads the physical bytes to Azure Blob Storage, and then saves the searchable metadata (including the new Blob URI) into Cosmos DB using our generic repository.

Create the file:

```bash
touch src/FileNetPOC.Services/Features/Documents/Commands/UploadDocument/UploadDocumentCommandHandler.cs

```

Add this code to `UploadDocumentCommandHandler.cs`:

```csharp
using Azure.Storage.Blobs;
using FileNetPOC.Shared.CQRS;
using FileNetPOC.Shared.Entities;
using FileNetPOC.Shared.Interfaces;
using Microsoft.Extensions.Configuration;

namespace FileNetPOC.Services.Features.Documents.Commands.UploadDocument;

public class UploadDocumentCommandHandler : ICommandHandler<UploadDocumentCommand, string>
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly IRepository<DocumentMetadata> _metadataRepository;
    private readonly string _containerName;

    public UploadDocumentCommandHandler(
        BlobServiceClient blobServiceClient,
        IRepository<DocumentMetadata> metadataRepository,
        IConfiguration configuration)
    {
        _blobServiceClient = blobServiceClient;
        _metadataRepository = metadataRepository;
        // Defaults to 'documents' if the config is missing
        _containerName = configuration["FileNet:BlobContainerName"] ?? "documents"; 
    }

    public async Task<string> Handle(UploadDocumentCommand request, CancellationToken cancellationToken)
    {
        // 1. Ensure the Blob Container exists
        var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
        await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        // 2. Generate a unique blob name to prevent overwriting files with the same name
        var blobName = $"{Guid.NewGuid()}-{request.FileName}";
        var blobClient = containerClient.GetBlobClient(blobName);
        
        // 3. Upload the byte array to Azure Blob Storage
        using var stream = new MemoryStream(request.FileContent);
        await blobClient.UploadAsync(stream, overwrite: true, cancellationToken);

        // 4. Create the Cosmos DB metadata record
        var metadata = new DocumentMetadata
        {
            FileName = request.FileName,
            BlobUri = blobClient.Uri.ToString(),
            ContentType = request.ContentType,
            FileSize = request.FileSize,
            Author = request.Author,
            UploadedAt = DateTime.UtcNow
        };

        // 5. Save to Cosmos DB using the generic repository we built earlier
        var createdDocument = await _metadataRepository.AddAsync(metadata, metadata.PartitionKey);

        // 6. Return the newly generated Cosmos DB ID to the API
        return createdDocument.id;
    }
}

```

### Verification

You now have a complete, secure, and performant upload feature. Run a build from your root folder to ensure everything wires together perfectly:

```bash
cd ~/Dev/FileNetPOC
dotnet build

```
