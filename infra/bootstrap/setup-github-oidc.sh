#!/usr/bin/env bash
# Configura el acceso de GitHub Actions a Azure SIN contraseñas ni tokens (OIDC federado).
# Ejecutar UNA sola vez en Azure Cloud Shell (portal.azure.com -> icono ">_" -> Bash):
#
#   curl -sSL https://raw.githubusercontent.com/UPT-FAING-EPIS/si784-2026-ii-si784-2026-ii-examen-u1-laos19/main/infra/bootstrap/setup-github-oidc.sh | bash
#
# Crea una identidad administrada con credencial federada para la rama main del repositorio
# y le da permisos sobre la suscripción. Al final imprime los 3 valores (no son secretos)
# que se registran como *Variables* del repositorio en GitHub.
set -euo pipefail

REPO="${GITHUB_REPO:-UPT-FAING-EPIS/si784-2026-ii-si784-2026-ii-examen-u1-laos19}"
LOCATION="${AZURE_LOCATION:-eastus2}"
RG="rg-musicstream-github"
IDENTITY="id-musicstream-github"

SUBSCRIPTION_ID=$(az account show --query id -o tsv)
TENANT_ID=$(az account show --query tenantId -o tsv)
echo "Suscripción: $(az account show --query name -o tsv) ($SUBSCRIPTION_ID)"

echo "Registrando proveedores de recursos..."
for ns in Microsoft.Web Microsoft.ContainerRegistry Microsoft.DBforPostgreSQL Microsoft.Network \
          Microsoft.Storage Microsoft.Insights Microsoft.OperationalInsights Microsoft.ManagedIdentity; do
  az provider register --namespace "$ns" --output none
done

echo "Creando identidad administrada $IDENTITY..."
az group create --name "$RG" --location "$LOCATION" --output none
az identity create --name "$IDENTITY" --resource-group "$RG" --location "$LOCATION" --output none
CLIENT_ID=$(az identity show --name "$IDENTITY" --resource-group "$RG" --query clientId -o tsv)
PRINCIPAL_ID=$(az identity show --name "$IDENTITY" --resource-group "$RG" --query principalId -o tsv)

echo "Creando credencial federada para repo:${REPO}:ref:refs/heads/main..."
if ! az identity federated-credential show --name github-main --identity-name "$IDENTITY" --resource-group "$RG" --output none 2>/dev/null; then
  az identity federated-credential create \
    --name github-main \
    --identity-name "$IDENTITY" \
    --resource-group "$RG" \
    --issuer "https://token.actions.githubusercontent.com" \
    --subject "repo:${REPO}:ref:refs/heads/main" \
    --audiences "api://AzureADTokenExchange" \
    --output none
fi

echo "Asignando rol Owner sobre la suscripción (necesario para asignar AcrPull al App Service)..."
for attempt in 1 2 3 4 5 6; do
  if az role assignment create \
      --assignee-object-id "$PRINCIPAL_ID" \
      --assignee-principal-type ServicePrincipal \
      --role Owner \
      --scope "/subscriptions/${SUBSCRIPTION_ID}" \
      --output none 2>/dev/null; then
    break
  fi
  echo "  esperando a que la identidad se propague (${attempt}/6)..."
  sleep 15
done

cat <<RESULT

Listo. Registra estas Variables en GitHub
(Settings -> Secrets and variables -> Actions -> pestaña "Variables" -> New repository variable):

  AZURE_CLIENT_ID       = ${CLIENT_ID}
  AZURE_TENANT_ID       = ${TENANT_ID}
  AZURE_SUBSCRIPTION_ID = ${SUBSCRIPTION_ID}

No son secretos: GitHub Actions obtiene un token temporal en cada ejecución (OIDC).
Luego ejecuta los workflows "infra" y después "deploy".
RESULT
