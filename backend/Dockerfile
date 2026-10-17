# syntax=docker/dockerfile:1

# ---------- Etapa de compilación ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0-noble@sha256:35f39dc26de234f9bd24d482b8c7a9391d263dfbc1e62f24bb59de1168dd8d21 AS build
WORKDIR /src

COPY MusicStream.sln ./
COPY src/MusicStream.Api/MusicStream.Api.csproj src/MusicStream.Api/packages.lock.json src/MusicStream.Api/
RUN dotnet restore src/MusicStream.Api/MusicStream.Api.csproj --locked-mode

COPY src/MusicStream.Api/ src/MusicStream.Api/
RUN dotnet publish src/MusicStream.Api/MusicStream.Api.csproj \
      -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ---------- Imagen final (Azure Linux distroless: sin shell ni gestor de paquetes, usuario no root) ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0-azurelinux3.0-distroless@sha256:6d7c5efaf40e11b600cf48ecc3c9e216ddf8ecc905d9755faaf0dbd9c0cf3935 AS final
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true \
    ConnectionStrings__Default="Data Source=/home/app/musicstream.db"

# Los binarios quedan como propiedad de root (solo lectura para el usuario "app").
# En Azure se usa PostgreSQL; SQLite (por defecto) escribe en el home del usuario no root.
COPY --from=build /app/publish/ ./

USER app
EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
  CMD ["dotnet", "MusicStream.Api.dll", "--healthcheck"]

ENTRYPOINT ["dotnet", "MusicStream.Api.dll"]
