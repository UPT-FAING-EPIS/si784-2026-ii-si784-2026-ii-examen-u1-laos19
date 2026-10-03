#!/usr/bin/env bash
# Ejecuta pruebas con cobertura y el análisis de SonarQube (C#, JS, Terraform, Docker).
# Requiere SONAR_HOST_URL y SONAR_TOKEN (generados por sonarqube-start.sh o SonarCloud).
# Opcional: SONAR_PROJECT_KEY (por defecto "musicstream") y SONAR_ORGANIZATION (SonarCloud).
set -euo pipefail

# Riesgo aceptado y documentado (terraform:S6329): el SKU Basic de Azure Container Registry
# no permite deshabilitar el acceso público. Se mitiga con admin_enabled=false (sin usuario/clave)
# y descarga por identidad administrada (AcrPull). PostgreSQL sí es privado.
# Riesgo aceptado (terraform:S6380): la API es pública por diseño (catálogo, registro, login)
# y aplica su propia autenticación JWT con roles; Easy Auth de App Service la duplicaría.

PROJECT_KEY="${SONAR_PROJECT_KEY:-musicstream}"
ROOT="$(git rev-parse --show-toplevel)"
cd "$ROOT"

extra=()
if [ -n "${SONAR_ORGANIZATION:-}" ]; then
  extra+=("/o:${SONAR_ORGANIZATION}")
fi
if [ -n "${JAVA_HOME:-}" ]; then
  extra+=("/d:sonar.scanner.skipJreProvisioning=true")
fi

(cd frontend && npm ci && npm run test:coverage)

dotnet sonarscanner begin \
  /k:"$PROJECT_KEY" \
  /n:"MusicStream" \
  "${extra[@]}" \
  /d:sonar.token="$SONAR_TOKEN" \
  /d:sonar.host.url="$SONAR_HOST_URL" \
  /d:sonar.projectBaseDir="$ROOT" \
  /d:sonar.cs.opencover.reportsPaths="**/TestResults/**/coverage.opencover.xml" \
  /d:sonar.javascript.lcov.reportPaths="frontend/coverage/lcov.info" \
  /d:sonar.exclusions="**/bin/**,**/obj/**,**/node_modules/**,frontend/dist/**,frontend/coverage/**,docs/**" \
  /d:sonar.coverage.exclusions="backend/src/MusicStream.Api/Program.cs,backend/src/MusicStream.Api/Dtos/**,backend/tools/**,frontend/src/pages/**,frontend/src/components/**,frontend/src/main.jsx,frontend/src/App.jsx,frontend/vite.config.js,**/*.test.js,.github/scripts/**" \
  /d:sonar.cpd.exclusions="backend/tests/**" \
  /d:sonar.issue.ignore.multicriteria=acr,appauth \
  /d:sonar.issue.ignore.multicriteria.acr.ruleKey="terraform:S6329" \
  /d:sonar.issue.ignore.multicriteria.acr.resourceKey="infra/terraform/main.tf" \
  /d:sonar.issue.ignore.multicriteria.appauth.ruleKey="terraform:S6380" \
  /d:sonar.issue.ignore.multicriteria.appauth.resourceKey="infra/terraform/main.tf" \
  /d:sonar.qualitygate.wait=true

dotnet build backend/MusicStream.sln --configuration Release
rm -rf backend/tests/MusicStream.Tests/TestResults
dotnet test backend/MusicStream.sln --configuration Release --no-build \
  --collect:"XPlat Code Coverage" \
  -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=opencover

dotnet sonarscanner end /d:sonar.token="$SONAR_TOKEN"
