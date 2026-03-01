Ah, I completely understand. You want true Infrastructure-as-Code (IaC) harmony where Terraform provisions the cloud resources and automatically hands the connection strings over to your .NET application without any manual copy-pasting. That is the exact right mindset for automation.

Since you want to maintain the ability to easily debug in VS Code (just pressing F5), writing a bash script to export environment variables can be clunky.

Instead, we will use a highly effective pattern: **We will have Terraform dynamically generate a .json configuration file directly into your .NET project directory every time it runs.** Your .NET app will be configured to read this auto-generated file.

Here is how we automate the handshake between your Infrastructure and your API.

### **Step 1: Tell Terraform to generate the config file**

Open your `infrastructure/main.tf` file and add this local_file resource at the very bottom. This tells Terraform to take the sensitive outputs and write them directly into your API's source code folder.

Terraform
```
# Auto-generate local configuration for the .NET API  
resource "local_file" "dotnet_config" {  
  content = jsonencode({  
    "FileNet" = {  
      "CosmosConnectionString"      = azurerm_cosmosdb_account.cosmos.primary_sql_connection_string  
      "AppInsightsConnectionString" = azurerm_application_insights.appinsights.connection_string  
    }  
  })  
  # Places the file right next to your appsettings.json  
  filename = "${path.module}/../src/FileNetPOC.Api/appsettings.Terraform.json"  
}
```
### **Step 2: Apply the Terraform update**

Run the apply command again in your infrastructure folder. Because this is just a local file creation, it will execute instantly.

Bash
```
terraform apply -auto-approve
```
*Verify that `appsettings.Terraform.json` has now magically appeared inside your src/FileNetPOC.Api folder with your actual Azure keys inside it.*

### **Step 3: Tell .NET to read the generated file**

Now we just need to tell your .NET Web API to load this new file into its configuration pipeline, overriding any empty default values.

Open your `src/FileNetPOC.Api/Program.cs` file and add this single line right after `var builder = WebApplication.CreateBuilder(args);`:

C#
```
var builder = WebApplication.CreateBuilder(args);

// --- ADD THIS LINE ---  
// Loads the auto-generated Terraform config. "optional: true" means it won't crash in production if the file isn't there.  
builder.Configuration.AddJsonFile("appsettings.Terraform.json", optional: true, reloadOnChange: true);  
// ---------------------

// 1. Add services to the container.  
builder.Services.AddControllers();  
// ... (rest of your Program.cs remains exactly the same)
```
### **Step 4: Secure your Git Repository**

Since appsettings.Terraform.json now contains your actual Sandbox database keys, we absolutely do not want to commit it to GitHub.

Run this command from the root of your FileNetPOC project to add it to your .gitignore:

Bash
```
echo "appsettings.Terraform.json" \>\> .gitignore
```
### **The Result**

You now have a fully automated loop\! Whenever your Sandbox gets destroyed and you run terraform apply to create a new one, Terraform will instantly overwrite appsettings.Terraform.json with the new keys. You can just hit F5 in VS Code or run dotnet run, and your API will instantly connect to the new database without you touching a single config file.

Are you ready to create the DocumentService.cs class that will actually upload files to Azurite and save the metadata to this newly wired-up Cosmos DB?