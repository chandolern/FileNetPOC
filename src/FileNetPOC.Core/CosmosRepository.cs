using System.Net;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using FileNetPOC.Shared.Entities;
using FileNetPOC.Shared.Interfaces;

namespace FileNetPOC.Core.Repositories;

public class CosmosRepository<T> : IRepository<T> where T : BaseEntity
{
    private readonly Container _container;

    public CosmosRepository(CosmosClient cosmosClient, IConfiguration configuration)
    {
        // Pulling the configuration we set up via Terraform
        var databaseName = configuration["FileNet:CosmosDatabaseName"];
        var containerName = configuration["FileNet:CosmosContainerName"]; 

        if (string.IsNullOrEmpty(databaseName) || string.IsNullOrEmpty(containerName))
        {
            throw new ArgumentNullException("Cosmos DB configuration is missing database or container names.");
        }

        _container = cosmosClient.GetContainer(databaseName, containerName);
    }

    public async Task<T> GetByIdAsync(string id, string partitionKey)
    {
        try
        {
            ItemResponse<T> response = await _container.ReadItemAsync<T>(id, new PartitionKey(partitionKey));
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // Return null instead of throwing an exception if the document doesn't exist
            return null!;
        }
    }

    public async Task<IEnumerable<T>> GetAllAsync(string partitionKey)
    {
        // Queries all items within the specified partition
        var query = _container.GetItemQueryIterator<T>(
            new QueryRequestOptions { PartitionKey = new PartitionKey(partitionKey) }
        );

        var results = new List<T>();
        while (query.HasMoreResults)
        {
            var response = await query.ReadNextAsync();
            results.AddRange(response.ToList());
        }

        return results;
    }

    public async Task<T> AddAsync(T entity, string partitionKey)
    {
        ItemResponse<T> response = await _container.CreateItemAsync(entity, new PartitionKey(partitionKey));
        return response.Resource;
    }

    public async Task UpdateAsync(T entity, string partitionKey)
    {
        // Upsert will replace the document if it exists, or create it if it doesn't
        await _container.UpsertItemAsync(entity, new PartitionKey(partitionKey));
    }

    public async Task DeleteAsync(string id, string partitionKey)
    {
        await _container.DeleteItemAsync<T>(id, new PartitionKey(partitionKey));
    }
}