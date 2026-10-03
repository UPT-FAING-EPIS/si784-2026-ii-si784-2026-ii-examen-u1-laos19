#!/usr/bin/env bash
# Levanta un servidor SonarQube Community efímero dentro del runner y genera
# credenciales propias (contraseña de admin aleatoria + token de análisis).
# No requiere ninguna cuenta ni token externo. Exporta SONAR_HOST_URL y SONAR_TOKEN.
# Los datos van a un tmpfs: el disco del runner suele superar el umbral de Elasticsearch.
set -euo pipefail

IMAGE="${SONARQUBE_IMAGE:-sonarqube:community}"
PORT="${SONARQUBE_PORT:-9000}"
HOST="http://localhost:${PORT}"

# Límites de descriptores para Elasticsearch (SONARQUBE_DOCKER_ARGS="" los desactiva).
read -r -a docker_args <<< "${SONARQUBE_DOCKER_ARGS---ulimit nofile=131072:131072}"
docker run -d --name sonarqube -p "${PORT}:9000" "${docker_args[@]}" \
  --tmpfs /opt/sonarqube/data:rw,size=4g,uid=1000,gid=0 \
  -e SONAR_ES_BOOTSTRAP_CHECKS_DISABLE=true \
  -e SONAR_TELEMETRY_ENABLE=false \
  "$IMAGE" >/dev/null

echo "Esperando a que SonarQube esté disponible..."
for _ in $(seq 1 90); do
  status=$(curl -fsS "${HOST}/api/system/status" 2>/dev/null | python3 -c 'import json,sys;print(json.load(sys.stdin).get("status",""))' 2>/dev/null || true)
  if [ "$status" = "UP" ]; then break; fi
  sleep 5
done
if [ "${status:-}" != "UP" ]; then
  docker logs --tail 100 sonarqube
  echo "SonarQube no inició" >&2
  exit 1
fi

# Contraseña de administrador generada en cada ejecución.
admin_password="$(openssl rand -hex 24)Aa1!"
curl -fsS -u admin:admin -X POST "${HOST}/api/users/change_password" \
  --data-urlencode "login=admin" \
  --data-urlencode "previousPassword=admin" \
  --data-urlencode "password=${admin_password}" >/dev/null

token=$(curl -fsS -u "admin:${admin_password}" -X POST "${HOST}/api/user_tokens/generate" \
  --data-urlencode "name=ci-$(date +%s)" \
  --data-urlencode "type=GLOBAL_ANALYSIS_TOKEN" | python3 -c 'import json,sys;print(json.load(sys.stdin)["token"])')

if [ -n "${GITHUB_ENV:-}" ]; then
  echo "::add-mask::${admin_password}"
  echo "::add-mask::${token}"
  {
    echo "SONAR_HOST_URL=${HOST}"
    echo "SONAR_TOKEN=${token}"
    echo "SONAR_ADMIN_PASSWORD=${admin_password}"
  } >> "$GITHUB_ENV"
else
  echo "export SONAR_HOST_URL=${HOST} SONAR_TOKEN=${token} SONAR_ADMIN_PASSWORD=${admin_password}"
fi
echo "SonarQube listo en ${HOST}"
