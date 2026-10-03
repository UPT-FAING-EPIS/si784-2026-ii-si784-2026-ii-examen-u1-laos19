variable "subscription_id" {
  description = "ID de la suscripción de Azure."
  type        = string
}

variable "project" {
  description = "Prefijo del proyecto usado en los nombres de recursos."
  type        = string
  default     = "musicstream"
}

variable "suffix" {
  description = "Sufijo único (minúsculas y números) para nombres globales como ACR."
  type        = string

  validation {
    condition     = can(regex("^[a-z0-9]{3,12}$", var.suffix))
    error_message = "El sufijo debe tener entre 3 y 12 caracteres en minúsculas o números."
  }
}

variable "location" {
  description = "Región principal de Azure."
  type        = string
  default     = "eastus2"
}

variable "static_web_app_location" {
  description = "Región de Azure Static Web Apps (westus2, centralus, eastus2, westeurope, eastasia)."
  type        = string
  default     = "eastus2"
}

variable "app_service_sku" {
  description = "SKU del App Service Plan Linux."
  type        = string
  default     = "B1"
}

variable "postgres_sku" {
  description = "SKU del servidor PostgreSQL Flexible."
  type        = string
  default     = "B_Standard_B1ms"
}

variable "admin_email" {
  description = "Correo del administrador inicial de la aplicación."
  type        = string
  default     = "admin@musicstream.pe"
}

variable "admin_password" {
  description = "Contraseña del administrador inicial de la aplicación."
  type        = string
  sensitive   = true
}

variable "image_tag" {
  description = "Tag inicial de la imagen del backend en ACR."
  type        = string
  default     = "latest"
}

variable "tags" {
  description = "Etiquetas comunes."
  type        = map(string)
  default = {
    project = "musicstream"
    course  = "SI784"
    managed = "terraform"
  }
}
