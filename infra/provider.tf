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