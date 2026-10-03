# MusicStream — Gestión de suscripciones a plataforma de música

Plataforma web para suscribirse, gestionar y disfrutar de streaming de música. Los administradores controlan planes, usuarios, catálogo y reportes.

| | |
|---|---|
| **Backend** | ASP.NET Core 8 Web API · EF Core · PostgreSQL (prod) / SQLite (dev) · JWT · xUnit |
| **Frontend** | React 19 + Vite · React Router · Vitest |
| **Contenedor** | `backend/Dockerfile` (multi-stage, runtime `aspnet:8.0-azurelinux3.0-distroless`, usuario no root, HEALTHCHECK) |
| **Nube** | Microsoft Azure (App Service Linux, ACR, PostgreSQL Flexible Server privado, Static Web Apps) |
| **IaC** | Terraform (`infra/terraform`) |
| **CI/CD** | GitHub Actions (`.github/workflows`) |

## Enlaces de entrega

| | URL |
|---|---|
| Aplicación publicada (frontend) | `https://<host>.azurestaticapps.net`: aparece en el resumen del workflow **deploy** |
| API publicada (Swagger) | `https://app-musicstream-laos19-api.azurewebsites.net/swagger` |
| Repositorio | https://github.com/UPT-FAING-EPIS/si784-2026-ii-si784-2026-ii-examen-u1-laos19 |
| Sonar | Ejecución del workflow **sonar** en la pestaña *Actions* (resumen + artefacto `sonarqube-report`) |

## Funcionalidades

- **Catálogo**: artistas, álbumes y canciones con búsqueda, filtro por tipo y género, y paginación.
- **Streaming**: reproductor integrado. Las canciones *Premium* requieren un plan que las incluya.
- **Suscripciones**: plan Free automático al registrarse, planes de pago, renovación (extiende la vigencia), cambio de plan, cancelación y renovación automática al vencer.
- **Playlists**: crear, agregar/quitar canciones y eliminar. El número máximo depende del plan.
- **Historial y recomendaciones**: recomendaciones por artistas y géneros escuchados, con respaldo por popularidad.
- **Administración**: CRUD de planes, gestión de usuarios (activar o desactivar, cambiar rol), suscripciones, catálogo y reportes de **uso**, **suscripciones** e **ingresos**.
- **Validación** en frontend (`frontend/src/lib/validation.js`) y backend (DataAnnotations + reglas de negocio → `ProblemDetails`).

## API REST

| Método | Ruta | Descripción | Acceso |
|---|---|---|---|
| `POST` | `/auth/register` · `/auth/login` | Registro / inicio de sesión (JWT) | Público |
| `GET` | `/music` | Catálogo (`search`, `type`, `genre`, `page`, `pageSize`) | Público |
| `GET` | `/music/{id}` | Detalle de canción, álbum o artista | Público |
| `POST` | `/music/{id}/play` | URL de streaming + registro en el historial | Usuario |
| `POST` | `/subscriptions` | Crear o renovar suscripción | Usuario |
| `GET` | `/subscriptions/{userId}` | Suscripción activa | Propietario/Admin |
| `DELETE` | `/subscriptions/{id}` | Cancelar suscripción | Propietario/Admin |
| `POST` | `/playlists` | Crear playlist | Usuario |
| `GET` | `/playlists/{userId}` | Playlists del usuario | Propietario/Admin |
| `GET` | `/history/{userId}` · `/recommendations/{userId}` | Historial y recomendaciones | Propietario/Admin |
| `POST` | `/reports` | Generar reporte (`Usage`, `Subscriptions`, `Revenue`) | Admin |
| `GET/POST/PUT/DELETE` | `/plans` | Planes | Lectura pública / escritura Admin |
| `GET/PATCH` | `/users` | Gestión de usuarios | Admin |

La documentación interactiva completa está en `/swagger`.

## Ejecución local

```bash
# Backend (SQLite) -> http://localhost:5080/swagger
cd backend
Seed__AdminEmail=admin@musicstream.pe Seed__AdminPassword='<clave-admin>' \
  dotnet run --project src/MusicStream.Api --launch-profile http

# Frontend -> http://localhost:5173 (proxy /api -> :5080)
cd frontend && npm install && npm run dev

# Alternativa con contenedores (API + PostgreSQL)
POSTGRES_PASSWORD='<clave-db>' ADMIN_PASSWORD='<clave-admin>' docker compose up --build
```

### Pruebas

```bash
dotnet test backend/MusicStream.sln     # 44 pruebas unitarias + de integración (WebApplicationFactory)
cd frontend && npm run test:coverage    # pruebas de validación y cliente HTTP
```

## Configuración de CI/CD (sin tokens)

| Workflow | Propósito | Necesita configurar |
|---|---|---|
| [`sonar.yml`](.github/workflows/sonar.yml) | Levanta un **SonarQube Community efímero** en el runner, genera sus propias credenciales, analiza C#, JS, Terraform y Docker con cobertura y falla si hay bugs, vulnerabilidades o hotspots. Publica el reporte HTML/JSON/MD | Nada |
| [`snyk-semgrep.yml`](.github/workflows/snyk-semgrep.yml) | **Semgrep** (SAST) y **Trivy** (dependencias NuGet/npm, secretos, Dockerfile/Terraform e imagen del contenedor). Reportes como artefactos y en *Security → Code scanning* | Nada |
| [`generate-documentation.yml`](.github/workflows/generate-documentation.yml) | Diccionario de datos y diagramas ER, de clases, de componentes y de despliegue (Mermaid) en [`docs/`](docs/README.md) | Nada |
| [`infra.yml`](.github/workflows/infra.yml) | Terraform: estado remoto y RG, VNet, PostgreSQL privado, ACR, App Service, Static Web App, Application Insights. **Genera todas las contraseñas** (BD, JWT, admin) | Azure (una vez) |
| [`deploy.yml`](.github/workflows/deploy.yml) | Pruebas, imagen → ACR → App Service, frontend → Static Web Apps | Azure (una vez) |

### Conectar Azure una sola vez (sin contraseñas)

Azure es el único servicio que necesita tu cuenta, porque los recursos se crean en tu suscripción. No se guarda ninguna contraseña: GitHub obtiene un token temporal en cada ejecución (OIDC federado).

1. Abre [portal.azure.com](https://portal.azure.com) → icono **Cloud Shell** (`>_`) → **Bash**, y ejecuta:
   ```bash
   curl -sSL https://raw.githubusercontent.com/UPT-FAING-EPIS/si784-2026-ii-si784-2026-ii-examen-u1-laos19/main/infra/bootstrap/setup-github-oidc.sh | bash
   ```
2. El script imprime 3 valores. Regístralos en GitHub → *Settings → Secrets and variables → Actions → **Variables*** (no son secretos): `AZURE_CLIENT_ID`, `AZURE_TENANT_ID` y `AZURE_SUBSCRIPTION_ID`.
3. En *Actions* ejecuta **infra** y luego **deploy**.

Opcionales: `AZURE_SUFFIX` (por defecto `laos19`) y `AZURE_LOCATION` (por defecto `eastus2`).
Contraseña del administrador (`admin@musicstream.pe`): Portal de Azure → App Service `app-musicstream-laos19-api` → *Configuración* → `Seed__AdminPassword`, o bien:
```bash
az webapp config appsettings list -g rg-musicstream-laos19 -n app-musicstream-laos19-api --query "[?name=='Seed__AdminPassword'].value" -o tsv
```

**Integraciones opcionales** (si se agregan, se usan automáticamente): secreto `SONAR_TOKEN` + variables `SONAR_ORGANIZATION`/`SONAR_PROJECT_KEY` para enviar el análisis a SonarCloud (dashboard permanente), y secreto `SNYK_TOKEN` para ejecutar también Snyk.

### Seguridad: riesgos aceptados

| Regla | Motivo |
|---|---|
| `terraform:S6329` (ACR con acceso público) | El SKU Basic de Azure Container Registry no permite deshabilitar el acceso público. Mitigado: sin usuario administrador y descarga por identidad administrada (`AcrPull`). PostgreSQL sí es privado (VNet) |
| `terraform:S6380` / Trivy `AZU-0003` (App Service sin Easy Auth) | La API es pública por diseño (catálogo, registro y login) y aplica su propia autenticación JWT con roles |

## Estructura

```
backend/
  src/MusicStream.Api/       API (Domain, Data, Dtos, Services, Controllers, Infrastructure)
  tests/MusicStream.Tests/   Pruebas unitarias (Unit/) e integración (Integration/)
  tools/MusicStream.DocGen/  Generador de documentación Mermaid
  Dockerfile
frontend/                    SPA React (pages/, components/, lib/)
infra/terraform/             Infraestructura Azure
infra/bootstrap/             Configuración única de OIDC GitHub → Azure
docs/                        Documentación generada
.github/workflows/           Automatizaciones
.github/scripts/             SonarQube efímero y reporte
```

