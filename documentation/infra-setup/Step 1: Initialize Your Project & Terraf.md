Step 1: Initialize Your Project & Terraform
Since you are on elementary OS with VS Code, Docker, and Terraform ready, let's get your GitHub repository set up with the foundational infrastructure code.

Open your terminal and clone your repo if you haven't already:

Bash
git clone https://github.com/chandolern/FileNetPOC.git
cd FileNetPOC
Create a new folder for your infrastructure:

Bash
mkdir infrastructure
cd infrastructure
1. Create providers.tf
This tells Terraform to use Azure. Create this file and add the following:

Terraform
terraform {
  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 3.0"
    }
  }
}

provider "azurerm" {
  features {}
  # Important: Bypasses the need for provider registration which is often blocked in sandboxes
  skip_provider_registration = true 
}
2. Create main.tf
This is where we define the resources. Notice how we use data to reference your existing sandbox resource group instead of creating one.

Terraform
# 1. READ the existing sandbox resource group
data "azurerm_resource_group" "rg" {
  name = "rg_sb_centralindia_287527_3_177238220330"
}

# Generate a random suffix to ensure globally unique names for Storage/ACR
resource "random_integer" "suffix" {
  min = 10000
  max = 99999
}

# 2. Storage Account (For the physical documents)
resource "azurerm_storage_account" "filenet_storage" {
  name                     = "filenetstore${random_integer.suffix.result}"
  resource_group_name      = data.azurerm_resource_group.rg.name
  location                 = data.azurerm_resource_group.rg.location
  account_tier             = "Standard"
  account_replication_type = "LRS" # Keep it cheap for the POC
}

# Container inside the storage account
resource "azurerm_storage_container" "documents" {
  name                  = "documents"
  storage_account_name  = azurerm_storage_account.filenet_storage.name
  container_access_type = "private"
}

# 3. Azure Container Registry (To hold your .NET Docker image)
resource "azurerm_container_registry" "acr" {
  name                = "filenetacr${random_integer.suffix.result}"
  resource_group_name = data.azurerm_resource_group.rg.name
  location            = data.azurerm_resource_group.rg.location
  sku                 = "Basic"
  admin_enabled       = true
}

# 4. Azure Kubernetes Service (AKS)
resource "azurerm_kubernetes_cluster" "aks" {
  name                = "filenet-aks-cluster"
  location            = data.azurerm_resource_group.rg.location
  resource_group_name = data.azurerm_resource_group.rg.name
  dns_prefix          = "filenetaks"

  default_node_pool {
    name       = "default"
    node_count = 1 # Single node for POC to save sandbox quota
    vm_size    = "Standard_B2s" # Small, cheap VM size
  }

  identity {
    type = "SystemAssigned"
  }
}

# 5. Allow AKS to pull images from ACR
resource "azurerm_role_assignment" "aks_to_acr" {
  principal_id                     = azurerm_kubernetes_cluster.aks.kubelet_identity[0].object_id
  role_definition_name             = "AcrPull"
  scope                            = azurerm_container_registry.acr.id
  skip_service_principal_aad_check = true
}
Step 2: Authenticate and Apply
To run this on your elementary OS machine, you need to log in to Azure via the Azure CLI, specifically targeting your lab tenant if necessary.

Log in to Azure:

Bash
az login
(Ensure it logs you in as labuser_287527_1772382195697@instructorwhizlabs.onmicrosoft.com. If you have multiple directories, make sure you are in the Whizlabs one).

Initialize and Run Terraform:

Bash
terraform init
terraform plan
Check the output of terraform plan. It should say it will create 6 resources (Random string, Storage Account, Container, ACR, AKS, Role Assignment).

Deploy:

Bash
terraform apply -auto-approve
What's Next?