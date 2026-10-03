output "resource_group_name" {
  value = azurerm_resource_group.main.name
}

output "acr_name" {
  value = azurerm_container_registry.main.name
}

output "acr_login_server" {
  value = azurerm_container_registry.main.login_server
}

output "api_app_name" {
  value = azurerm_linux_web_app.api.name
}

output "api_url" {
  value = "https://${azurerm_linux_web_app.api.default_hostname}"
}

output "static_web_app_name" {
  value = azurerm_static_web_app.frontend.name
}

output "frontend_url" {
  value = "https://${azurerm_static_web_app.frontend.default_host_name}"
}

output "postgres_fqdn" {
  value = azurerm_postgresql_flexible_server.main.fqdn
}
