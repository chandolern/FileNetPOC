using FileNetPOC.Shared.CQRS;

namespace FileNetPOC.Services.Features.Documents.Commands.UploadDocument;

/// <summary>
/// Command to upload a new document. 
/// Returns the newly generated Cosmos DB Document ID (string) upon success.
/// </summary>
public record UploadDocumentCommand(
    string FileName,
    string ContentType,
    long FileSize,
    byte[] FileContent,
    string Author
) : ICommand<string>;