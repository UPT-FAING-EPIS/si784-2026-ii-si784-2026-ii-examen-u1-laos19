# syntax=docker/dockerfile:1

# ---------- Etapa de compilación ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0-noble@sha256:e70cdb7f80b0348f5cb85f19a8f670fca061f033d57eed12fa003d58b0e06317 AS build
WORKDIR /src

COPY MusicStream.sln ./
COPY src/MusicStream.Api/MusicStream.Api.csproj src/MusicStream.Api/
RUN dotnet restore src/MusicStream.Api/MusicStream.Api.csproj

COPY src/MusicStream.Api/ src/MusicStream.Api/
RUN dotnet publish src/MusicStream.Api/MusicStream.Api.csproj \
      -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ---------- Imagen final (chiseled: sin shell ni gestor de paquetes, usuario no root) ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0-noble-chiseled@sha256:a5a4846e78cd50d3d71c780bff1fc212349e514e8e4bf7658638b48a72083b43 AS final
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

ENTRYPOINT ["dotnet", "MusicStream.Api.dll"]
