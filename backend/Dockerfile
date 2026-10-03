# syntax=docker/dockerfile:1

# ---------- Etapa de compilación ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0-noble@sha256:536b706b6113a3839c1712ebcf832dc33d275fd605d35a931db956792c10d26c AS build
WORKDIR /src

COPY MusicStream.sln ./
COPY src/MusicStream.Api/MusicStream.Api.csproj src/MusicStream.Api/
RUN dotnet restore src/MusicStream.Api/MusicStream.Api.csproj

COPY src/MusicStream.Api/ src/MusicStream.Api/
RUN dotnet publish src/MusicStream.Api/MusicStream.Api.csproj \
      -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ---------- Imagen final (chiseled: sin shell ni gestor de paquetes, usuario no root) ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled@sha256:48e51f2f6798897be7ac4e775c049ed8fe60d3190f637e1f9c9dc7513efa659c AS final
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
