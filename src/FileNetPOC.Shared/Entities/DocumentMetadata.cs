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