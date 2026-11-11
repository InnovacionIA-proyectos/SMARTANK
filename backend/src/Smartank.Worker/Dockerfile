# SMARTANK - Worker (.NET 10): sincronizaciones programadas contra el SQL Server legado y Geotab.
# Contexto de build: la carpeta /backend (referencias entre proyectos).
#   docker build -f src/Smartank.Worker/Dockerfile -t smartank-worker .   (desde /backend)
# En Dokploy (app tipo Application / Dockerfile): Build Path = backend, Dockerfile = src/Smartank.Worker/Dockerfile.
# No expone puertos: solo consume el SQL Server legado/Geotab y escribe en la base de SMARTANK.

# ---- Etapa de build --------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/Smartank.Domain/Smartank.Domain.csproj", "src/Smartank.Domain/"]
COPY ["src/Smartank.Application/Smartank.Application.csproj", "src/Smartank.Application/"]
COPY ["src/Smartank.Infrastructure/Smartank.Infrastructure.csproj", "src/Smartank.Infrastructure/"]
COPY ["src/Smartank.Worker/Smartank.Worker.csproj", "src/Smartank.Worker/"]
RUN dotnet restore "src/Smartank.Worker/Smartank.Worker.csproj"

COPY src/ src/
RUN dotnet publish "src/Smartank.Worker/Smartank.Worker.csproj" \
    -c Release -o /app/publish /p:UseAppHost=false

# ---- Etapa de runtime -------------------------------------------------------
# Imagen "runtime" (sin ASP.NET): el Worker no sirve HTTP.
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app

# TLS heredado para el Data Warehouse corporativo (SQL Server antiguo), igual que en la imagen del API:
# solo negocia TLS 1.0 y las bases Debian endurecidas lo rechazan. Archivo propio apuntado con OPENSSL_CONF.
RUN printf '%s\n' \
        'openssl_conf = openssl_init' \
        '' \
        '[openssl_init]' \
        'ssl_conf = ssl_sect' \
        '' \
        '[ssl_sect]' \
        'system_default = system_default_sect' \
        '' \
        '[system_default_sect]' \
        'CipherString = DEFAULT:@SECLEVEL=0' \
        'MinProtocol = TLSv1' \
        'Options = UnsafeLegacyRenegotiation' \
        > /etc/ssl/openssl-legacy-tls.cnf
ENV OPENSSL_CONF=/etc/ssl/openssl-legacy-tls.cnf

# Ejecuta como usuario sin privilegios (incluido en la imagen base).
USER $APP_UID

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Smartank.Worker.dll"]
