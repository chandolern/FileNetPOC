Let's dive into the **`FileNetPOC.Services`** project. This is where your actual business logic lives.

To keep this project highly organized and maintainable as it grows, we are going to use **Feature Folder** (or Vertical Slice) organization. Instead of dumping all commands in one folder and all handlers in another, we will group them by the actual feature they perform.

Our first feature will be: **Uploading a Document**.

### Step 1: Create the Feature Folder Structure

Open your terminal and navigate to your `Services` project. Let's create a dedicated space for our Document features, specifically the Upload command.

```bash
cd ~/Dev/FileNetPOC/src/FileNetPOC.Services
mkdir -p Features/Documents/Commands/UploadDocument

```

### Step 2: Create the Command

The Command is a simple, immutable data object. It holds exactly the information the system needs to perform the action—nothing more, nothing less.

Since it's immutable, we will use a C# `record` instead of a `class`.

Create the file:

```bash
touch Features/Documents/Commands/UploadDocument/UploadDocumentCommand.cs

```

Open `UploadDocumentCommand.cs` and add this code. Notice how it inherits from the strict `ICommand<TResponse>` interface we built in your `Shared` library:

```csharp
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

```

*(Note: We are using `byte[] FileContent` here instead of `IFormFile`. This keeps your Services layer pure. `IFormFile` is an ASP.NET Core web type, and remember: we don't want our Services layer to know it's being called by a Web API!)*

### Step 3: Quick Build

Run a quick compile from the root to make sure the namespaces and interface inheritance are happy:

```bash
cd ~/Dev/FileNetPOC
dotnet build

```
This is a classic "missing package" build error, and it actually highlights something great: your `Shared` and `Core` projects built perfectly!

The `Services` project is throwing these errors because it doesn't have the Azure Blob Storage SDK or the Microsoft Configuration extensions installed yet. While we registered `BlobServiceClient` and `IConfiguration` over in your `Api` project's `Program.cs`, the `Services` class library needs its own references to those packages to compile the code we just wrote.

Let's fix this by adding the two missing packages directly to the `Services` project.

### Step 1: Install the Missing Packages

Open your terminal and run these commands:

```bash
cd ~/Dev/FileNetPOC/src/FileNetPOC.Services
dotnet add package Azure.Storage.Blobs
dotnet add package Microsoft.Extensions.Configuration.Abstractions

```

### Step 2: Rebuild

Once those packages are installed, head back to your root directory and run the build again:

```bash
cd ~/Dev/FileNetPOC
dotnet build

```

That should completely clear up the `CS0246` and `CS0234` errors. Let me know if you get a clean build this time, and we can finally wire up the API Controller to trigger this pipeline!