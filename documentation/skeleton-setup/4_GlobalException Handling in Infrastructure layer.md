You are thinking exactly like a software architect right now by mapping out how dependencies and runtime executions flow between your projects.

To answer your question directly: **No, an exception bubbling up from the Core or Service layer would not create a circular reference.** A circular reference happens at *compile-time* (e.g., Project A references Project B, and Project B tries to reference Project A). Exceptions, however, are handled at *runtime*. When the Core layer throws an exception, it just halts execution and bubbles up the call stack until it finds a `catch` block, regardless of where that block lives.

However, your instinct that putting `CustomExceptionHandler` in the `Shared` library is dangerous is **100% correct**, but for a different architectural reason. It causes what we call **"Framework Pollution."**

Here is exactly why that file is forbidden from living in your `Shared` library:

## 1. The "Web" Contamination Problem

The `IExceptionHandler` interface requires ASP.NET Core libraries. It forces you to interact with `HttpContext`, `HttpResponse`, and HTTP Status Codes (like 400 Bad Request or 500 Internal Server Error).

If you put this in your `Shared` project, you must install the ASP.NET Core framework into `Shared`.
Because `Core` and `Services` both reference `Shared`, **your entire database and business logic layers are now infected with Web API dependencies.**

## 2. The Clean Architecture Violation

In Clean Architecture, your inner rings (`Shared`, `Core`, `Services`) must remain blissfully unaware of how the outside world communicates with them. Your database should not know what an "HTTP 500" is. It only knows C#.

* **Inner Layers (`Shared`, `Core`, `Services`):** Throw pure C# exceptions (e.g., `ValidationException`, `ArgumentNullException`, `CosmosException`).
* **Outer Layer (`Api`):** Acts as the translator. It catches those pure C# exceptions and translates them into web-friendly HTTP responses (`ProblemDetails`).

## 3. The "Future-Proof" Scenario

Imagine six months from now, you need to write a background worker (like an Azure Function or a RabbitMQ consumer) that processes documents silently in the background without a web interface.

You would want to reuse your `Core` and `Services` libraries for that background worker. If your `Shared` library contained `HttpContext` logic, your background worker would crash or fail to compile because there is no HTTP request in a background service!

By keeping `CustomExceptionHandler` strictly inside the `FileNetPOC.Api` project, your `Shared`, `Core`, and `Services` projects remain pure, portable C# libraries that can be plugged into any application type (Web, Mobile, Background Worker, Desktop).

---

Does that clarify why the API layer is the only safe place for HTTP exception handling?


---

This is the final piece of our foundational architecture. By setting up a global Exception Handler at the very edge of your API, your core services and MediatR handlers never have to worry about `try/catch` blocks for standard HTTP responses. They just throw exceptions, and this class catches them, formatting a beautiful, standardized JSON response.

We will build this in the **`FileNetPOC.Api`** project, as it directly deals with HTTP requests and responses.

### Step 1: Create the Infrastructure Folder

Open your terminal and navigate to your API project to create an `Infrastructure` folder for this cross-cutting web concern:

```bash
cd ~/Dev/FileNetPOC/src/FileNetPOC.Api
mkdir Infrastructure
touch Infrastructure/CustomExceptionHandler.cs

```

### Step 2: Implement the Exception Handler

Open `Infrastructure/CustomExceptionHandler.cs` and paste the following code.

Notice how it specifically looks for the `ValidationException` we set up in our Shared library. If it sees one, it automatically maps it to a **400 Bad Request** and attaches the specific property errors. If it's a database crash or null reference, it defaults to a **500 Internal Server Error**.

```csharp
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using FluentValidation;

namespace FileNetPOC.Api.Infrastructure;

public class CustomExceptionHandler : IExceptionHandler
{
    private readonly ILogger<CustomExceptionHandler> _logger;

    public CustomExceptionHandler(ILogger<CustomExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Exception occurred: {Message}", exception.Message);

        var problemDetails = new ProblemDetails
        {
            Instance = httpContext.Request.Path
        };

        if (exception is ValidationException validationException)
        {
            problemDetails.Title = "Validation Error";
            problemDetails.Status = StatusCodes.Status400BadRequest;
            problemDetails.Detail = "One or more validation errors occurred.";
            
            // Groups the FluentValidation errors nicely by property name
            problemDetails.Extensions["errors"] = validationException.Errors
                .GroupBy(x => x.PropertyName, x => x.ErrorMessage)
                .ToDictionary(x => x.Key, x => x.ToArray());
        }
        else
        {
            problemDetails.Title = "An unexpected error occurred";
            problemDetails.Status = StatusCodes.Status500InternalServerError;
            // Note: In a true production environment, you might hide exception.Message 
            // from the client for security, but it's great for our POC debugging.
            problemDetails.Detail = exception.Message; 
        }

        httpContext.Response.StatusCode = problemDetails.Status.Value;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        // Returning true tells the .NET pipeline that we successfully handled the crash
        return true; 
    }
}

```

### Step 3: Register the Handler in `Program.cs`

We need to tell the .NET 8 pipeline to actually use this new class.

Open `src/FileNetPOC.Api/Program.cs` and make two small additions:

**1. Add it to the Dependency Injection Container:**
Find the line where you registered your controllers (`builder.Services.AddControllers();`) and add these two lines right below it:

```csharp
builder.Services.AddControllers();
// --- ADD THESE TWO LINES ---
builder.Services.AddExceptionHandler<FileNetPOC.Api.Infrastructure.CustomExceptionHandler>();
builder.Services.AddProblemDetails();
// ---------------------------

```

**2. Add it to the Middleware Pipeline:**
Scroll down to the bottom where the HTTP request pipeline is configured (right above `app.UseHttpsRedirection();`) and add the `UseExceptionHandler` middleware:

```csharp
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// --- ADD THIS LINE ---
app.UseExceptionHandler();
// ---------------------

app.UseHttpsRedirection();
app.UseAuthorization();

```

### Step 4: Verify the Build

Since we added a reference to `FluentValidation` in the API project (to catch the exception), you might need to ensure the API project knows about it. Luckily, because the API references the `Services` project, which references `Shared` (where FluentValidation lives), the transitive dependency should cover it.

Run a build from the root of your repository to ensure it compiles:

```bash
cd ~/Dev/FileNetPOC
dotnet build

```
---
---

Yes, syntactically, the code you pasted is absolutely correct for wiring up the new .NET 8 global exception handler! You placed the service registrations and the middleware exactly where they belong in the pipeline.

**However, I want to give you a quick architectural warning:** It looks like the snippet you just pasted is a fresh .NET web template. If you completely overwrite your `Program.cs` with only that code, **you will lose the automated Terraform configuration and the Azure Cosmos DB/Blob Storage dependency injection we built earlier.**

To make sure you don't lose that hard work, here is the complete, merged `Program.cs` that includes both your new Exception Handler *and* our existing Azure Infrastructure setup:

```csharp
using Azure.Storage.Blobs;
using Microsoft.Azure.Cosmos;
using FileNetPOC.Api.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// 1. Load the auto-generated Terraform configuration (Crucial for your local/cloud hybrid setup)
builder.Configuration.AddJsonFile("appsettings.Terraform.json", optional: true, reloadOnChange: true);

// 2. Add Base Services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 3. Add Exception Handling (Your new additions)
builder.Services.AddExceptionHandler<CustomExceptionHandler>();
builder.Services.AddProblemDetails();

// 4. Bind the configuration section & Register Azure SDK Clients
var fileNetConfig = builder.Configuration.GetSection("FileNet");

// Blob Storage Client
builder.Services.AddSingleton(x => 
{
    var connectionString = fileNetConfig["StorageConnectionString"];
    return new BlobServiceClient(connectionString);
});

// Cosmos DB Client
builder.Services.AddSingleton(x =>
{
    var connectionString = fileNetConfig["CosmosConnectionString"];
    var options = new CosmosClientOptions()
    {
        SerializerOptions = new CosmosSerializationOptions()
        {
            PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
        },
        ConnectionMode = ConnectionMode.Direct
    };
    return new CosmosClient(connectionString, options);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// 5. Enable the Exception Handler Middleware (Your new addition)
app.UseExceptionHandler();

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();

```

If you paste this merged version in, run a quick `dotnet build` from your root folder.