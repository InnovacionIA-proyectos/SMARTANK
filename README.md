# SMARTANK

Gobierno del tanqueo de una flota de camiones de extremo a extremo: **decidir** donde y cuanto tanquear, **controlar** la
autorizacion, **ejecutar** la orden con el conductor y **verificar** la carga contra la telemetria del vehiculo.

Monorepo con backend en **ASP.NET 10 (Clean Architecture)**, frontend en **Angular 22** y base de datos **SQL Server 2019**,
desplegado en **Dokploy** mediante Dockerfile.

```
backend/    Solucion .NET (Domain, Application, Infrastructure, Api, Worker) y sus pruebas
frontend/   Aplicacion Angular 22 (SPA servida por nginx)
Database/   ddl.sql (esquema completo para SQL Server 2019), seed_estaciones.sql, scripts
docs/       Arquitectura, modelo de datos, API y pruebas
.github/    Workflows de CI/CD (GitHub Actions)
```

## Stack

| Capa | Tecnologia |
|---|---|
| Frontend | Angular 22, TypeScript, Tailwind CSS, Leaflet (mapas), graficos del tablero |
| Backend | C# / ASP.NET 10, Clean Architecture de 4 capas, monolito modular, Worker Service para tareas programadas |
| Acceso a datos | Entity Framework Core 10 (base propia), Dapper (SQL Server legado, solo lectura) |
| Base de datos | SQL Server 2019 (esquema en `Database/ddl.sql`, sin migraciones de EF) |
| Integraciones | Geotab (telemetria), API HTTP de WhatsApp (orden al conductor), SQL Server legado (flota, viajes, tanqueos, rutas) |
| Autenticacion | JWT en cookie `httpOnly`, contrasenas con bcrypt, roles Despacho / Control de Flota / Finanzas / Admin |
| Calidad | xUnit + Moq, pruebas de integracion contra SQL Server real, GitHub Actions |
| Contenedores | Docker (un Dockerfile por servicio), Docker Compose para local/QA, Dokploy para despliegue |

## Requisitos

- .NET SDK 10
- Node.js ^22.22.3 o ^24.15.0 (lo exige Angular 22) y npm
- Docker (para la base local y las imagenes)

## Ejecucion local con Docker Compose

```bash
cp .env.example .env          # completa DB_SA_PASSWORD, JWT_SECRET, BITACORA_SECRET y SERVICIO_SECRETO
docker compose up --build
```

| Servicio | URL |
|---|---|
| Frontend | http://localhost:4200 |
| API | http://localhost:8080 (`/health`, y `/swagger` si `ASPNETCORE_ENVIRONMENT=Development`) |
| SQL Server | `127.0.0.1,14333` (usuario `sa`) |

El servicio `db-init` crea la base y aplica `Database/ddl.sql` y `Database/seed_estaciones.sql` la primera vez. Para crear el
administrador inicial:

```bash
docker compose exec -e Admin__Email=admin@ejemplo.com -e Admin__Password='<clave-de-12+-caracteres>' -e Admin__Nombre=Administrador \
  api dotnet Smartank.Api.dll crear-admin
```

## Ejecucion local sin Docker (desarrollo)

Base de datos: crea una base vacia en tu SQL Server 2019 y ejecuta `Database/ddl.sql` (ver [Database/README.md](Database/README.md)).

```bash
# Backend (desde /backend)
export ConnectionStrings__Smartank="Server=localhost,1433;Database=Smartank;User Id=sa;Password=<clave>;TrustServerCertificate=True;"
export Jwt__Secret=<secreto> Bitacora__Secret=<secreto> Servicio__Secreto=<secreto> Cookie__Secure=false
dotnet run --project src/Smartank.Api                 # http://localhost:8080 (ver launchSettings)
dotnet run --project src/Smartank.Worker              # sincronizaciones programadas (opcional)

# Frontend (desde /frontend)
npm ci
npm start                                             # http://localhost:4200, proxy de /api hacia el API local
```

## Pruebas

```bash
# Backend: Domain y Application no necesitan base de datos
dotnet test backend/Smartank.sln -c Release

# Pruebas de integracion contra SQL Server real (esquema ya creado con ddl.sql)
export TEST_DB_CONNECTION="Server=127.0.0.1,14333;Database=SmartankTest;User Id=sa;Password=<clave>;TrustServerCertificate=True;Encrypt=False"
dotnet test backend/Smartank.sln -c Release

# Frontend
npm test --prefix frontend
```

Sin `TEST_DB_CONNECTION` las pruebas de integracion se omiten (no fallan). Mas detalle en [docs/PRUEBAS.md](docs/PRUEBAS.md).

## Documentacion

| Documento | Contenido |
|---|---|
| [docs/ARQUITECTURA.md](docs/ARQUITECTURA.md) | Capas, modulos, flujos y decisiones |
| [docs/MODELO_DE_DATOS.md](docs/MODELO_DE_DATOS.md) | Diccionario de datos y reglas de la base |
| [docs/API.md](docs/API.md) | Endpoints, roles, formatos y errores |
| [docs/PRUEBAS.md](docs/PRUEBAS.md) | Estrategia y ejecucion de pruebas |
| [DOKPLOY.md](DOKPLOY.md) | Despliegue en Dokploy y verificacion |
| [Database/README.md](Database/README.md) | Como crear el esquema (DBeaver, SSMS, sqlcmd) |
| [.env.example](.env.example), [.env.dokploy](.env.dokploy), [.env.githubSECRETS](.env.githubSECRETS) | Variables de entorno y secrets |

## Estructura del backend

```
backend/
  Smartank.sln
  src/
    Smartank.Domain/          Entidades, enums, reglas puras (autonomia, geometria)
    Smartank.Application/     Casos de uso (CasosDeUso/<Modulo>), DTOs, puertos
    Smartank.Infrastructure/  EF Core, Dapper (legado), Geotab, WhatsApp, tokens, hash
    Smartank.Api/             Controllers, autenticacion, middlewares, Dockerfile
    Smartank.Worker/          Tareas programadas, Dockerfile
  tests/
    Smartank.Domain.Tests/ Smartank.Application.Tests/ Smartank.Api.Tests/
```

## Convenciones

- Clases con prefijo `cls` (`clsVehiculo`), interfaces con prefijo `I`, constantes en MAYUSCULAS_SOSTENIDAS, variables en camelCase.
- Tablas con prefijo `tbl` y nombre en PascalCase plural (`tblVehiculos`).
- Casos de uso como `Command`/`Query` con su handler, sin mediador; sin microservicios.
- Secretos solo por variables de entorno; nunca en el repositorio.
