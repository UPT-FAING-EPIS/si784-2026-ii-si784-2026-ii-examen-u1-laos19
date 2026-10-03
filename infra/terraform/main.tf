locals {
  name          = "${var.project}-${var.suffix}"
  acr_name      = substr(replace("${var.project}${var.suffix}acr", "-", ""), 0, 50)
  database_name = "musicstream"
}

resource "azurerm_resource_group" "main" {
  name     = "rg-${local.name}"
  location = var.location
  tags     = var.tags
}

# ---------------------------------------------------------------------------
# Secretos generados (se almacenan cifrados en el estado remoto de Terraform)
# ---------------------------------------------------------------------------
resource "random_password" "postgres" {
  length           = 32
  special          = true
  override_special = "_-."
}

resource "random_password" "jwt" {
  length  = 64
  special = false
}

# Contraseña del administrador inicial de la aplicación (sin secretos manuales).
# Se consulta con: terraform output -raw admin_password  (o en Configuración del App Service).
resource "random_password" "admin" {
  length      = 20
  special     = false
  min_upper   = 2
  min_lower   = 2
  min_numeric = 2
}

# ---------------------------------------------------------------------------
# Registro de contenedores
# ---------------------------------------------------------------------------
resource "azurerm_container_registry" "main" {
  name                = local.acr_name
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  sku                 = "Basic"
  admin_enabled       = false
  # El SKU Basic no admite redes privadas; el acceso exige autenticación de Entra ID.
  public_network_access_enabled = true
  tags                          = var.tags

  identity {
    type = "SystemAssigned"
  }
}

# ---------------------------------------------------------------------------
# Red privada: el backend accede a PostgreSQL sin exposición pública
# ---------------------------------------------------------------------------
resource "azurerm_virtual_network" "main" {
  name                = "vnet-${local.name}"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  address_space       = ["10.20.0.0/16"]
  tags                = var.tags
}

resource "azurerm_subnet" "app" {
  name                 = "snet-app"
  resource_group_name  = azurerm_resource_group.main.name
  virtual_network_name = azurerm_virtual_network.main.name
  address_prefixes     = ["10.20.1.0/24"]

  delegation {
    name = "app-service"
    service_delegation {
      name    = "Microsoft.Web/serverFarms"
      actions = ["Microsoft.Network/virtualNetworks/subnets/action"]
    }
  }
}

resource "azurerm_subnet" "db" {
  name                 = "snet-db"
  resource_group_name  = azurerm_resource_group.main.name
  virtual_network_name = azurerm_virtual_network.main.name
  address_prefixes     = ["10.20.2.0/24"]
  service_endpoints    = ["Microsoft.Storage"]

  delegation {
    name = "postgres"
    service_delegation {
      name    = "Microsoft.DBforPostgreSQL/flexibleServers"
      actions = ["Microsoft.Network/virtualNetworks/subnets/join/action"]
    }
  }
}

resource "azurerm_private_dns_zone" "postgres" {
  name                = "${local.name}.private.postgres.database.azure.com"
  resource_group_name = azurerm_resource_group.main.name
  tags                = var.tags
}

resource "azurerm_private_dns_zone_virtual_network_link" "postgres" {
  name                  = "pdnslink-${local.name}"
  resource_group_name   = azurerm_resource_group.main.name
  private_dns_zone_name = azurerm_private_dns_zone.postgres.name
  virtual_network_id    = azurerm_virtual_network.main.id
  tags                  = var.tags
}

# ---------------------------------------------------------------------------
# Base de datos PostgreSQL Flexible Server (acceso privado)
# ---------------------------------------------------------------------------
resource "azurerm_postgresql_flexible_server" "main" {
  name                          = "psql-${local.name}"
  resource_group_name           = azurerm_resource_group.main.name
  location                      = azurerm_resource_group.main.location
  version                       = "16"
  sku_name                      = var.postgres_sku
  storage_mb                    = 32768
  backup_retention_days         = 7
  geo_redundant_backup_enabled  = true
  delegated_subnet_id           = azurerm_subnet.db.id
  private_dns_zone_id           = azurerm_private_dns_zone.postgres.id
  public_network_access_enabled = false
  administrator_login           = "musicadmin"
  administrator_password        = random_password.postgres.result
  tags                          = var.tags

  authentication {
    password_auth_enabled = true
  }

  depends_on = [azurerm_private_dns_zone_virtual_network_link.postgres]

  lifecycle {
    ignore_changes = [zone]
  }
}

resource "azurerm_postgresql_flexible_server_database" "main" {
  name      = local.database_name
  server_id = azurerm_postgresql_flexible_server.main.id
  charset   = "UTF8"
  collation = "en_US.utf8"
}

resource "azurerm_postgresql_flexible_server_configuration" "require_ssl" {
  name      = "require_secure_transport"
  server_id = azurerm_postgresql_flexible_server.main.id
  value     = "on"
}

# Auditoría y protección de conexiones.
resource "azurerm_postgresql_flexible_server_configuration" "log_connections" {
  name      = "log_connections"
  server_id = azurerm_postgresql_flexible_server.main.id
  value     = "on"
}

resource "azurerm_postgresql_flexible_server_configuration" "log_checkpoints" {
  name      = "log_checkpoints"
  server_id = azurerm_postgresql_flexible_server.main.id
  value     = "on"
}

resource "azurerm_postgresql_flexible_server_configuration" "connection_throttling" {
  name      = "connection_throttle.enable"
  server_id = azurerm_postgresql_flexible_server.main.id
  value     = "on"
}

# ---------------------------------------------------------------------------
# Frontend: Azure Static Web Apps
# ---------------------------------------------------------------------------
resource "azurerm_static_web_app" "frontend" {
  name                = "swa-${local.name}"
  resource_group_name = azurerm_resource_group.main.name
  location            = var.static_web_app_location
  sku_tier            = "Free"
  sku_size            = "Free"
  tags                = var.tags
}

# ---------------------------------------------------------------------------
# Backend: App Service Linux con contenedor
# ---------------------------------------------------------------------------
resource "azurerm_log_analytics_workspace" "main" {
  name                = "log-${local.name}"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  sku                 = "PerGB2018"
  retention_in_days   = 30
  tags                = var.tags
}

resource "azurerm_application_insights" "main" {
  name                = "appi-${local.name}"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  workspace_id        = azurerm_log_analytics_workspace.main.id
  application_type    = "web"
  tags                = var.tags
}

resource "azurerm_service_plan" "main" {
  name                = "asp-${local.name}"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  os_type             = "Linux"
  sku_name            = var.app_service_sku
  tags                = var.tags
}

resource "azurerm_linux_web_app" "api" {
  name                       = "app-${local.name}-api"
  resource_group_name        = azurerm_resource_group.main.name
  location                   = azurerm_resource_group.main.location
  service_plan_id            = azurerm_service_plan.main.id
  virtual_network_subnet_id  = azurerm_subnet.app.id
  https_only                 = true
  client_certificate_enabled = true
  client_certificate_mode    = "Optional"
  tags                       = var.tags

  identity {
    type = "SystemAssigned"
  }

  site_config {
    always_on                               = true
    ftps_state                              = "Disabled"
    http2_enabled                           = true
    minimum_tls_version                     = "1.2"
    health_check_path                       = "/health"
    health_check_eviction_time_in_min       = 5
    container_registry_use_managed_identity = true

    application_stack {
      docker_image_name   = "musicstream-api:${var.image_tag}"
      docker_registry_url = "https://${azurerm_container_registry.main.login_server}"
    }
  }

  # La autenticación la implementa la propia API (JWT + roles); el catálogo y /health
  # son públicos por diseño, por eso no se habilita la autenticación de plataforma (Easy Auth).

  logs {
    detailed_error_messages = false
    failed_request_tracing  = true

    http_logs {
      file_system {
        retention_in_days = 7
        retention_in_mb   = 35
      }
    }

    application_logs {
      file_system_level = "Warning"
    }
  }

  app_settings = {
    WEBSITES_PORT                         = "8080"
    WEBSITES_ENABLE_APP_SERVICE_STORAGE   = "false"
    ASPNETCORE_ENVIRONMENT                = "Production"
    APPLICATIONINSIGHTS_CONNECTION_STRING = azurerm_application_insights.main.connection_string
    Database__Provider                    = "Postgres"
    ConnectionStrings__Default            = "Host=${azurerm_postgresql_flexible_server.main.fqdn};Port=5432;Database=${local.database_name};Username=${azurerm_postgresql_flexible_server.main.administrator_login};Password=${random_password.postgres.result};SSL Mode=Require"
    Jwt__Key                              = random_password.jwt.result
    Seed__AdminEmail                      = var.admin_email
    Seed__AdminPassword                   = random_password.admin.result
    Cors__AllowedOrigins__0               = "https://${azurerm_static_web_app.frontend.default_host_name}"
  }

  lifecycle {
    # El tag de la imagen lo actualiza deploy.yml en cada despliegue.
    ignore_changes = [site_config[0].application_stack[0].docker_image_name]
  }
}

# La identidad del App Service puede descargar imágenes desde ACR.
resource "azurerm_role_assignment" "acr_pull" {
  scope                = azurerm_container_registry.main.id
  role_definition_name = "AcrPull"
  principal_id         = azurerm_linux_web_app.api.identity[0].principal_id
}
