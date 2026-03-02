namespace FileNetPOC.Shared.Entities;

public abstract class BaseEntity
{
    // Cosmos DB relies heavily on this unique identifier.
    public string id { get; set; } = Guid.NewGuid().ToString();
}