# Diagrama de despliegue

> Documento generado automáticamente por `MusicStream.DocGen` (workflow `generate-documentation.yml`). No editar manualmente.

Recursos declarados en Terraform (`infra/terraform`):

- `azurerm_application_insights.main`
- `azurerm_container_registry.main`
- `azurerm_linux_web_app.api`
- `azurerm_log_analytics_workspace.main`
- `azurerm_postgresql_flexible_server.main`
- `azurerm_postgresql_flexible_server_configuration.require_ssl`
- `azurerm_postgresql_flexible_server_configuration.log_connections`
- `azurerm_postgresql_flexible_server_configuration.log_checkpoints`
- `azurerm_postgresql_flexible_server_configuration.connection_throttling`
- `azurerm_postgresql_flexible_server_database.main`
- `azurerm_private_dns_zone.postgres`
- `azurerm_private_dns_zone_virtual_network_link.postgres`
- `azurerm_resource_group.main`
- `azurerm_role_assignment.acr_pull`
- `azurerm_service_plan.main`
- `azurerm_static_web_app.frontend`
- `azurerm_subnet.app`
- `azurerm_subnet.db`
- `azurerm_virtual_network.main`
- `random_password.postgres`
- `random_password.jwt`
- `random_password.admin`

```mermaid
flowchart TB
    dev([Desarrollador]) -->|git push| gh[GitHub Repository]
    gh -.OIDC sin secretos.-> AZ
    subgraph GHA["GitHub Actions"]
        infra[infra.yml<br/>Terraform]
        deploy[deploy.yml]
        sonar[sonar.yml]
        sec[snyk-semgrep.yml]
        docs[generate-documentation.yml]
    end
    gh --> GHA
    sonar --> sc[(SonarQube efímero<br/>o SonarCloud)]
    sec --> snyk[(Semgrep / Trivy<br/>Snyk opcional)]
    browser([Navegador del usuario])
    subgraph AZ["Microsoft Azure - Resource Group"]
        swa[Azure Static Web Apps<br/>Frontend React]
        acr[(Azure Container Registry<br/>musicstream-api)]
        subgraph VNET["Virtual Network 10.20.0.0/16"]
            subgraph SAPP["snet-app"]
                app[App Service Linux<br/>contenedor .NET 8 distroless<br/>Managed Identity]
            end
            subgraph SDB["snet-db"]
                pg[(PostgreSQL Flexible Server 16<br/>acceso privado)]
            end
        end
        dns[Private DNS Zone]
        ai[Application Insights<br/>+ Log Analytics]
    end
    infra -->|aprovisiona| AZ
    deploy -->|docker push| acr
    deploy -->|build + upload| swa
    acr -->|AcrPull| app
    browser -->|HTTPS| swa
    browser -->|HTTPS REST + JWT| app
    app -->|TLS 5432| pg
    app -.-> dns
    app -.telemetría.-> ai
```
