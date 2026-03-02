This is a massive architectural upgrade. Moving to a Clean Architecture model with CQRS and generic repositories right from the start is exactly how enterprise applications are built. Having a dedicated shared library for generic interfaces will make your unit testing (using tools like Moq or NSubstitute) infinitely easier later on.

Since we are taking this strictly step-by-step, our singular goal right now is to scaffold this exact project structure using the .NET CLI, set up the project dependencies, and tie them all together in a single solution.

Here is the blueprint for the architecture we are about to create:

* **FileNetPOC.Shared**: A class library holding your generic IRepository\<T\>, your base generic CQRS interfaces (ICommand, IQuery), and generic base entities.  
* **FileNetPOC.Core**: A class library for your database implementation (where the actual Cosmos DB generic repository logic will live).  
* **FileNetPOC.Services**: A class library for your CQRS Handlers (the business logic that executes when a command or query is dispatched).  
* **FileNetPOC.Api**: Your Web API presentation layer, which will remain incredibly thin, doing nothing but routing HTTP requests to your CQRS pipeline.

### **Step 1: Scaffold the Multi-Project Structure**

Open your terminal in VS Code. Let's navigate to your src folder and build this out. Run these commands sequentially:

Bash
```
# Ensure you are in the src directory  
cd ~/Dev/FileNetPOC/src

# 1. Create the Shared, Core, and Services class libraries  
dotnet new classlib \-n FileNetPOC.Shared  
dotnet new classlib \-n FileNetPOC.Core  
dotnet new classlib \-n FileNetPOC.Services

# 2. Create the Web API project   
# (Using --force because appsettings.Terraform.json is already in this folder)  
cd FileNetPOC.Api  
dotnet new webapi --force  
cd ..
```
### **Step 2: Wire Up the Project Dependencies**

Now we need to tell .NET how these projects relate to each other so the compiler allows them to share code.

Bash
```
# Core needs the interfaces from Shared  
dotnet add FileNetPOC.Core/FileNetPOC.Core.csproj reference FileNetPOC.Shared/FileNetPOC.Shared.csproj

# Services need the data access from Core and interfaces from Shared  
dotnet add FileNetPOC.Services/FileNetPOC.Services.csproj reference FileNetPOC.Core/FileNetPOC.Core.csproj  
dotnet add FileNetPOC.Services/FileNetPOC.Services.csproj reference FileNetPOC.Shared/FileNetPOC.Shared.csproj

# The API needs access to the Services to dispatch HTTP requests  
dotnet add FileNetPOC.Api/FileNetPOC.Api.csproj reference FileNetPOC.Services/FileNetPOC.Services.csproj
```
### **Step 3: Create and Populate the Solution File**

Finally, we will create a .sln file at the root of your repository so you can easily manage all four projects at once in VS Code.

Bash
```
# Move back to the root of your repository  
cd ~/Dev/FileNetPOC

# Create the solution file  
dotnet new sln \-n FileNetPOC

# Add all four projects to the solution  
dotnet sln add src/FileNetPOC.Shared/FileNetPOC.Shared.csproj  
dotnet sln add src/FileNetPOC.Core/FileNetPOC.Core.csproj  
dotnet sln add src/FileNetPOC.Services/FileNetPOC.Services.csproj  
dotnet sln add src/FileNetPOC.Api/FileNetPOC.Api.csproj
```
### **Verification Test**

To ensure everything is linked correctly and there are no compilation errors from the initial scaffolding, run a build from the root directory:

Bash
```
dotnet build
```