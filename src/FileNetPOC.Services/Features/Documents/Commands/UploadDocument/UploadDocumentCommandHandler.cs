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