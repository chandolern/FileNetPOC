variable "resource_group_name" {
  description = "The name of the pre-configured Azure Sandbox resource group"
  type        = string
}

variable "project_prefix" {
  description = "A prefix for all deployed resources"
  type        = string
  default     = "filenet"
}

variable "environment" {
  description = "The deployment environment (e.g., dev, it, uat, prod)"
  type        = string
  default     = "dev"
}

variable "location" {
  description = "Azure region"
  type        = string
  default     = "centralindia"
}