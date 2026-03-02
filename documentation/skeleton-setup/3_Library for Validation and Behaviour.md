Using `Shared` instead of spinning up a whole new `BuildingBlocks` project is a great call. It prevents "project bloat" while still keeping your architecture cleanly separated.

Since you asked for improvements, I do have one crucial architectural suggestion regarding exactly *where* inside your solution these three specific files should live.

Here is the golden rule of Clean Architecture: **Your `Shared`, `Core`, and `Services` projects should never know they are part of a Web API.** They should be pure C# logic.

Because of this, I suggest we split these cross-cutting concerns into two locations:

### 1. The MediatR Behaviors -> `FileNetPOC.Shared`

Logging and Validation are pure business logic. They intercept the MediatR `IRequest` before it hits your handler. They belong perfectly in your `Shared` project.

* `src/FileNetPOC.Shared/Behaviors/LoggingBehavior.cs`
* `src/FileNetPOC.Shared/Behaviors/ValidationBehavior.cs`

### 2. The Exception Handler -> `FileNetPOC.Api`

The `IExceptionHandler` interfaces directly with HTTP Requests, HTTP Status Codes (like 400 Bad Request or 500 Internal Server Error), and the `HttpContext`. If we put this in `Shared`, we would have to force your pure C# library to depend on ASP.NET Core web libraries, which violates clean architecture.

* `src/FileNetPOC.Api/Infrastructure/CustomExceptionHandler.cs`

---

If that separation makes sense to you, let's start by building the **Logging Behavior** in your `Shared` project. It is the easiest to set up and immediately gives you performance tracking for every database call you make.

### Step 1: Add the Logging Abstraction

Run this command to give your `Shared` project access to the standard `ILogger` interface (without tying it to a specific logging provider like Serilog or Application Insights just yet):

```bash
cd ~/Dev/FileNetPOC/src/FileNetPOC.Shared
dotnet add package Microsoft.Extensions.Logging.Abstractions

```

### Step 2: Create the File

Create the folder and file for the behavior:

```bash
mkdir Behaviors
touch Behaviors/LoggingBehavior.cs

```

### Step 3: Implement the Logging Pipeline

Open `Behaviors/LoggingBehavior.cs` and add this code. This intercepts every command and query, logs its name, starts a timer, runs the actual handler, and logs how long it took.

```csharp
using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FileNetPOC.Shared.Behaviors;

public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        
        _logger.LogInformation("Handling Command/Query: {RequestName}", requestName);
        
        var timer = Stopwatch.StartNew();
        
        // This executes the actual handler (or the next behavior in the pipeline)
        var response = await next();
        
        timer.Stop();
        
        _logger.LogInformation("Handled Command/Query: {RequestName} in {ElapsedMilliseconds} ms", requestName, timer.ElapsedMilliseconds);
        
        return response;
    }
}

```

Run a quick `dotnet build` from your root folder. 

Awesome. With the logging behavior compiling cleanly, your application is now capable of tracking the execution time of every single command and query without polluting your actual business logic.

Now, let's build the **Gatekeeper**: the Validation Behavior.

This behavior will automatically intercept incoming requests. If a request is invalid (e.g., missing a filename or the file size is 0 bytes), it will instantly throw an exception and short-circuit the pipeline, ensuring bad data never reaches your database repository.

### Step 1: Install FluentValidation

We will use FluentValidation, which is the industry standard for building strongly typed validation rules in .NET.

Run this command in your terminal to add it to your `Shared` project:

```bash
cd ~/Dev/FileNetPOC/src/FileNetPOC.Shared
dotnet add package FluentValidation.DependencyInjectionExtensions

```

### Step 2: Create the Validation Behavior

You already have the `Behaviors` folder, so let's just create the file:

```bash
touch Behaviors/ValidationBehavior.cs

```

### Step 3: Implement the Gatekeeper Logic

Open `Behaviors/ValidationBehavior.cs` and add this code.

Notice how it injects `IEnumerable<IValidator<TRequest>>`. This means it will automatically find all the specific validation rules we write later (like an `UploadDocumentCommandValidator`) and execute them before calling `next()`.

```csharp
using FluentValidation;
using MediatR;

namespace FileNetPOC.Shared.Behaviors;

public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (_validators.Any())
        {
            var context = new ValidationContext<TRequest>(request);

            // Run all validators asynchronously
            var validationResults = await Task.WhenAll(
                _validators.Select(v => v.ValidateAsync(context, cancellationToken)));

            // Gather any validation failures
            var failures = validationResults
                .Where(r => r.Errors.Any())
                .SelectMany(r => r.Errors)
                .ToList();

            if (failures.Any())
            {
                // Instantly halts the pipeline. Our Global Exception Handler will catch this later
                // and automatically format a clean 400 Bad Request JSON response for the client.
                throw new ValidationException(failures);
            }
        }

        // If validation passes, move to the next step (either another behavior or the actual handler)
        return await next();
    }
}

```

### Step 4: Verify the Build

Run a quick build from the root of your repository to ensure FluentValidation is resolving correctly:

```bash
cd ~/Dev/FileNetPOC
dotnet build

```

If this builds successfully, your `Shared` project is officially complete and architecturally bulletproof!

Would you like me to guide you through setting up the Global Exception Handler in the API layer next so it can gracefully catch these validation exceptions?