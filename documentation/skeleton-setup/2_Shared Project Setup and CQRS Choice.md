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

Spot on. You are absolutely right.

Adding explicit handlers (`IQueryHandler`, `ICommandHandler`), enforcing the `notnull` constraint on responses, and leveraging MediatR's `Unit` for void commands makes the pipeline significantly more robust and type-safe. It also keeps your `Services` layer much cleaner, as you won't have to pepper `IRequestHandler` everywhere.

Let's update the files we just created to use your refined implementation.

### 1. Update `IQuery.cs`

Open `src/FileNetPOC.Shared/CQRS/IQuery.cs` and replace its contents with your improved version (just ensuring the namespace matches our project):

```csharp
using MediatR;

namespace FileNetPOC.Shared.CQRS;

public interface IQuery<out TResponse> : IRequest<TResponse>  
    where TResponse : notnull
{
}

// Handlers
public interface IQueryHandler<in TQuery, TResponse>
    : IRequestHandler<TQuery, TResponse>
    where TQuery : IQuery<TResponse>
    where TResponse : notnull
{
}

```

### 2. Update `ICommand.cs`

Open `src/FileNetPOC.Shared/CQRS/ICommand.cs` and replace it with your explicit Unit and Handler implementation:

```csharp
using MediatR;

namespace FileNetPOC.Shared.CQRS;

public interface ICommand : ICommand<Unit>
{
}

public interface ICommand<out TResponse> : IRequest<TResponse>
{
}

// Handlers
public interface ICommandHandler<in TCommand> 
    : ICommandHandler<TCommand, Unit>
    where TCommand : ICommand<Unit>
{ 
}

public interface ICommandHandler<in TCommand, TResponse> 
    : IRequestHandler<TCommand, TResponse> 
    where TCommand : ICommand<TResponse>
    where TResponse : notnull
{
}

```

### 3. Quick Build

Run a quick `dotnet build` from the root directory to make sure MediatR resolves the `Unit` type correctly and the constraints compile without issue.

---

I completely understand the desire to lock down those contracts with explicit `in` and `out` modifiers. It is excellent domain-driven design practice to enforce that a handler only ever consumes (`in`) a command and never returns it.

However, here is the technical reality of why the C# compiler threw that `CS1961` error, and why we actually **cannot** use the `in` keyword here if we are using the latest versions of MediatR.

## The MediatR v12+ Variance Change

In MediatR version 12, the creator (Jimmy Bogard) intentionally introduced a breaking change that **removed the `in` and `out` variance modifiers** from `IRequest` and `IRequestHandler`. He did this because explicit variance was causing massive generic resolution bugs with modern Dependency Injection containers.

Because MediatR's base `IRequestHandler<TRequest, TResponse>` is now strictly **invariant** (it has no `in` or `out`), the C# compiler dictates that any interface inheriting from it must also be invariant for those specific type parameters.

If you try to force `in TQuery` on your interface, C# blocks it because you are trying to pass a contravariant type into an invariant base interface.

### The Strictest Possible Implementation

To maintain the tightest contracts possible while keeping the C# compiler and MediatR happy, we must drop the `in` keyword on the handlers, but we can keep the strict generic constraints (`where TQuery : IQuery<TResponse>`) and the `notnull` enforcements.

Here is the final, strictest, perfectly compiling version of your CQRS contracts:

**1. `CQRS/ICommand.cs**`

```csharp
using MediatR;

namespace FileNetPOC.Shared.CQRS;

public interface ICommand : ICommand<Unit>
{
}

public interface ICommand<out TResponse> : IRequest<TResponse>
{
}

// Handlers must be invariant to match MediatR v12+
public interface ICommandHandler<TCommand> 
    : ICommandHandler<TCommand, Unit>
    where TCommand : ICommand<Unit>
{ 
}

public interface ICommandHandler<TCommand, TResponse> 
    : IRequestHandler<TCommand, TResponse> 
    where TCommand : ICommand<TResponse>
    where TResponse : notnull
{
}

```

**2. `CQRS/IQuery.cs**`

```csharp
using MediatR;

namespace FileNetPOC.Shared.CQRS;

public interface IQuery<out TResponse> : IRequest<TResponse>  
    where TResponse : notnull
{
}

// Handlers must be invariant to match MediatR v12+
public interface IQueryHandler<TQuery, TResponse>
    : IRequestHandler<TQuery, TResponse>
    where TQuery : IQuery<TResponse>
    where TResponse : notnull
{
}

```

By using this setup, you still get all the architectural benefits: developers cannot accidentally mix up Queries and Commands, and responses are guaranteed to not be null.

With the `Shared` library fully locked in and compiling, we are ready to build the actual data access layer.

Would you like to move into the **`FileNetPOC.Core`** project next to install the Cosmos DB SDK and implement your generic `CosmosRepository<T>`?
