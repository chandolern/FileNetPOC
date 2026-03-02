using FileNetPOC.Shared.Entities;

namespace FileNetPOC.Shared.Interfaces;

// The generic constraint "where T : BaseEntity" ensures this repository 
// can only be used with our database models.
public interface IRepository<T> where T : BaseEntity
{
    Task<T> GetByIdAsync(string id, string partitionKey);
    Task<IEnumerable<T>> GetAllAsync(string partitionKey);
    Task<T> AddAsync(T entity, string partitionKey);
    Task UpdateAsync(T entity, string partitionKey);
    Task DeleteAsync(string id, string partitionKey);
}