# 1. READ the existing sandbox resource group dynamically
data "azurerm_resource_group" "rg" {
  name = var.resource_group_name
}

# 2. Log Analytics Workspace
resource "azurerm_log_analytics_workspace" "law" {
  name                = "${var.project_prefix}-law-${var.environment}"
  location            = data.azurerm_resource_group.rg.location
  resource_group_name = data.azurerm_resource_group.rg.name
  sku                 = "PerGB2018"
  retention_in_days   = 30
}

# 3. Application Insights
resource "azurerm_application_insights" "appinsights" {
  name                = "${var.project_prefix}-appinsights-${var.environment}"
  location            = data.azurerm_resource_group.rg.location
  resource_group_name = data.azurerm_resource_group.rg.name
  workspace_id        = azurerm_log_analytics_workspace.law.id
  application_type    = "web"
}

# Generate a random suffix for globally unique names
resource "random_integer" "suffix" {
  min = 1000
  max = 9999
}

# 4. Storage Account
resource "azurerm_storage_account" "filenet_storage" {
  # Name must be globally unique, lowercase, max 24 chars. 
  name                     = "${var.project_prefix}${var.environment}${random_integer.suffix.result}"
  resource_group_name      = data.azurerm_resource_group.rg.name
  location                 = data.azurerm_resource_group.rg.location
  account_tier             = "Standard"
  account_replication_type = "LRS" 
}

# 5. Cosmos DB Account
resource "azurerm_cosmosdb_account" "cosmos" {
  name                = "${var.project_prefix}-cosmos-${var.environment}-${random_integer.suffix.result}"
  location            = data.azurerm_resource_group.rg.location
  resource_group_name = data.azurerm_resource_group.rg.name
  offer_type          = "Standard"
  kind                = "GlobalDocumentDB"

  # --- ADD THIS LINE TO COMPLY WITH AZURE POLICY ---  
  access_key_metadata_writes_enabled = false  
  # -------------------------------------------------
  
  consistency_policy {
    consistency_level = "Session"
  }

  geo_location {
    location          = data.azurerm_resource_group.rg.location
    failover_priority = 0
  }
}

output "app_insights_connection_string" {
  value     = azurerm_application_insights.appinsights.connection_string
  sensitive = true
}

# Auto-generate local configuration for the .NET API
resource "local_file" "dotnet_config" {
  content = jsonencode({
    "FileNet" = {
      "CosmosConnectionString"      = azurerm_cosmosdb_account.cosmos.primary_sql_connection_string
      "AppInsightsConnectionString" = azurerm_application_insights.appinsights.connection_string
    }
  })
  
  # This path tells Terraform to go up one level from 'infra' and into the 'src' folder
  filename = "${path.module}/../src/FileNetPOC.Api/appsettings.Terraform.json"
}