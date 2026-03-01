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