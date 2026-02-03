variable "environment" {
  description = "Environment name (e.g. dev, prod)"
  type        = string
}

variable "location" {
  description = "Azure region"
  type        = string
  default     = "East US"
}

variable "name_suffix" {
  description = "Short unique suffix for globally unique names (e.g. dev tenant initials). Lowercase alphanumeric, 3-8 chars."
  type        = string
}
