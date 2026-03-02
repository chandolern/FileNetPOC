Now we will populate the **FileNetPOC.Shared** project. This library will hold the absolute core abstractions of your architecture. By defining these here, your Core (Database) and Services (Business Logic) projects can communicate without ever tightly coupling to each other.

Let's clean up the default files and create your generic Entity base class and generic Repository interface.

### **Step 1: Clean up default classes**

When we scaffolded the class libraries, .NET automatically created a Class1.cs file in each of them. Let's delete those to keep the workspace clean.

Run this from your terminal at the root of your FileNetPOC project:

Bash
```
rm src/FileNetPOC.Shared/Class1.cs  
rm src/FileNetPOC.Core/Class1.cs  
rm src/FileNetPOC.Services/Class1.cs
```
### **Step 2: Create the Base Entity**

Since Cosmos DB always requires an id property (specifically lowercase for the JSON serialization, though we can map it via attributes/options), creating a base entity ensures every database model you ever create adheres to this rule.

Create a folder named Entities inside the Shared project, and then create a BaseEntity.cs file:

Bash

```
mkdir -p src/FileNetPOC.Shared/Entities  
touch src/FileNetPOC.Shared/Entities/BaseEntity.cs
```

Open `BaseEntity.cs` and add this code:

C#
```
namespace FileNetPOC.Shared.Entities;

public abstract class BaseEntity  
{  
    // Cosmos DB relies heavily on this unique identifier.  
    public string id { get; set; } = Guid.NewGuid().ToString();  
}
```
### **Step 3: Create the Generic Repository Interface**

This interface defines the standard CRUD operations. Notice we include a partitionKey parameter in the methods, as this is a fundamental requirement for querying Cosmos DB efficiently.

Create an Interfaces folder in the Shared project, and then create IRepository.cs:

Bash
```
mkdir -p src/FileNetPOC.Shared/Interfaces  
touch src/FileNetPOC.Shared/Interfaces/IRepository.cs
```
Open `IRepository.cs` and add this code:

C#
```
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
```
### **Step 4: Verify the Abstractions**

Run a quick build to ensure the syntax is correct and the namespaces align.

Bash
```
dotnet build
```
Once you confirm the build succeeds, we have a design decision to make for the **CQRS implementation** in this Shared library.
