# SMARTANK - API ASP.NET Core (.NET 10). Build multi-stage: restore -> (test opcional) -> publish -> runtime minimo.
# Contexto de build: la carpeta /backend (referencias entre proyectos).
#   docker build -f src/Smartank.Api/Dockerfile -t smartank-api .            (desde /backend)
#   docker build -f src/Smartank.Api/Dockerfile --target test -t smartank-api-test .   (corre las pruebas unitarias)
# En Dokploy (app tipo Application / Dockerfile): Build Path = backend, Dockerfile = src/Smartank.Api/Dockerfile.

# ---- Etapa de build --------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copia primero los .csproj para aprovechar la cache de capas de Docker en el restore:
# si solo cambia codigo, no se repite el restore.
COPY ["src/Smartank.Domain/Smartank.Domain.csproj", "src/Smartank.Domain/"]
COPY ["src/Smartank.Application/Smartank.Application.csproj", "src/Smartank.Application/"]
COPY ["src/Smartank.Infrastructure/Smartank.Infrastructure.csproj", "src/Smartank.Infrastructure/"]
COPY ["src/Smartank.Api/Smartank.Api.csproj", "src/Smartank.Api/"]
RUN dotnet restore "src/Smartank.Api/Smartank.Api.csproj"

COPY src/ src/

# ---- Etapa de pruebas (opcional: solo con --target test) ---------------------
# Corre las pruebas que no necesitan base de datos (Domain y Application).
FROM build AS test
COPY tests/ tests/
RUN dotnet test tests/Smartank.Domain.Tests -c Release \
    && dotnet test tests/Smartank.Application.Tests -c Release

# ---- Etapa de publicacion ----------------------------------------------------
FROM build AS publish
RUN dotnet publish "src/Smartank.Api/Smartank.Api.csproj" \
    -c Release -o /app/publish /p:UseAppHost=false

# ---- Etapa de runtime -------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Sin curl a proposito: la imagen no depende de apt (ni de un mirror alcanzable) para construirse. El healthcheck de
# docker-compose usa bash puro (/dev/tcp) contra /health.

# TLS heredado para el Data Warehouse corporativo (SQL Server antiguo): solo negocia TLS 1.0, que las bases Debian
# endurecidas rechazan (SECLEVEL=2, MinProtocol=TLSv1.2). SQL Server cifra SIEMPRE el login, asi que `Encrypt=False`
# no evita el handshake y sin esto toda consulta al DW falla con "pre-login handshake ... Encryption(ssl/tls) handshake
# failed". Se usa un archivo PROPIO apuntado con OPENSSL_CONF (no depende del openssl.cnf de la base) que aplica solo
# dentro de este contenedor.
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

# La imagen aspnet:10.0 escucha en 8080 por defecto (ASPNETCORE_HTTP_PORTS=8080).
EXPOSE 8080

COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Smartank.Api.dll"]
