# Pruebas de SMARTANK

## 1. Estrategia

| Nivel | Proyecto | Necesita base de datos | Que cubre |
|---|---|---|---|
| Unitarias de dominio | `Smartank.Domain.Tests` | No | Reglas puras: autonomia, seleccion de EDS, verificacion de carga, maquina de estados, geometria, normalizaciones, metricas, estado del flete |
| Unitarias de aplicacion | `Smartank.Application.Tests` | No | Casos de uso con Moq: casos felices, validaciones y cada excepcion con su `code` |
| Integracion y HTTP | `Smartank.Api.Tests` | Si (`TEST_DB_CONNECTION`) | Esquema `ddl.sql` contra el mapeo de EF, repositorios, adaptadores y el API completo por HTTP (cookie, roles, JSON, flujo de tanqueo) |
| Frontend | `frontend` (Vitest) | No | Utilidades, guards, interceptor, servicios con `HttpTestingController`, componentes y pantallas |

Las reglas de negocio puras (geometria, autonomia, recomendacion, verificacion) llevan pruebas con valores calculados a mano:
son la garantia de que el comportamiento coincide con el del sistema anterior.

## 2. Ejecucion

```bash
# Backend sin base de datos (Domain y Application); las de integracion se omiten sin TEST_DB_CONNECTION
dotnet test backend/Smartank.sln -c Release

# Backend completo, contra una base SQL Server 2019 con el esquema ya creado
export TEST_DB_CONNECTION="Server=127.0.0.1,14333;Database=SmartankTest;User Id=sa;Password=<clave>;TrustServerCertificate=True;Encrypt=False"
dotnet test backend/Smartank.sln -c Release

# Un modulo o un grupo
dotnet test backend/Smartank.sln -c Release --filter "FullyQualifiedName~Smartank.Api.Tests.Http"

# Frontend
npm ci --prefix frontend
npm test --prefix frontend
```

## 3. Base de datos de pruebas

1. Crear una base **vacia y dedicada** (por ejemplo `SmartankTest`) con collation `Modern_Spanish_CI_AS` y compatibilidad 150.
2. Ejecutar `Database/ddl.sql` completo (con `sqlcmd -I` o DBeaver). Ver [Database/README.md](../Database/README.md).
3. Exportar `TEST_DB_CONNECTION` con un login que lea y escriba filas (no necesita DDL).

Contenedor local de ejemplo:

```bash
docker run -d --name smartank-sqltest -e ACCEPT_EULA=Y -e MSSQL_PID=Developer \
  -e MSSQL_SA_PASSWORD='<clave-fuerte>' -p 14333:1433 mcr.microsoft.com/mssql/server:2019-latest
```

Usar `127.0.0.1` y no `localhost` en la cadena de conexion desde Windows.

### Reglas que no se deben romper

- **Nunca** apuntar `TEST_DB_CONNECTION` a una base de produccion ni llamar `Database.EnsureDeleted()` desde las pruebas: el
  esquema lo crea una persona con `ddl.sql`, no las pruebas ni la aplicacion.
- Las pruebas de repositorio corren dentro de una transaccion que se revierte al terminar. Las pruebas HTTP **si dejan filas**
  (usuarios, viajes, autorizaciones, alertas) con marcas aleatorias, porque usan varias conexiones; por eso exigen una base
  dedicada. `tblAuditLog` es append-only y no se puede limpiar.
- El ensamblado deshabilita el paralelismo de pruebas (`clsSinParalelismo`): las pruebas de base mantienen transacciones
  abiertas y en paralelo se producian interbloqueos.

## 4. Pruebas HTTP del API

`clsApiFactory` levanta el API real en memoria (todo el pipeline: JSON snake_case, autenticacion por cookie, roles, limite de
solicitudes y manejo de errores). Cubren:

- `/health`, formato estandar de error y 401 sin sesion.
- Login (credenciales invalidas sin revelar el correo), cookie `HttpOnly` y `SameSite=Strict`, `me`, logout.
- Roles por endpoint (alertas, metricas, autorizaciones) y secreto de servicio de la ingesta de telemetria.
- Flujo completo del tanqueo: recomendar (con y sin cuerpo), autorizar, notificar, abrir la bitacora con el token, subir la foto
  (rechazando contenido que no es imagen), reportar la carga, verificacion automatica con alerta y rechazo del segundo reporte.
- Seguridad de la bitacora: token alterado o manifiesto usado como credencial responden 401.

## 5. CI

`.github/workflows/ci-cd-backend.yml` y `ci-cd-frontend.yml` ejecutan restore, build, revision de paquetes vulnerables y la suite
completa en el runner self-hosted. El backend corre contra la base real de pruebas definida en el secret `TEST_DB_CONNECTION`.
Detalle de secrets en [.env.githubSECRETS](../.env.githubSECRETS).

## 6. Lo que no esta cubierto

- Visualizacion en navegador de las pantallas, mapas (Leaflet) y graficos: solo se prueban en jsdom.
- No hay prueba E2E de navegador (el recorrido de Playwright del sistema anterior no se migro).
- Envio real a la API de WhatsApp y a la EDS: se prueba con un manejador HTTP simulado.
- Rendimiento con volumenes grandes (polilineas de miles de puntos, consulta de metricas con muchos tanqueos).
