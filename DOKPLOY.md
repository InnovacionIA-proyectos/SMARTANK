# Despliegue en Dokploy

El despliegue se hace con **3 apps tipo "Application" (Dockerfile) separadas** en Dokploy, no como "Docker Compose". Cada Dockerfile
del repositorio se registra como su propia app; las tres comparten la red interna que Dokploy crea para las apps de un mismo
proyecto. La base de datos NO es una app de Dokploy: es el SQL Server 2019 corporativo.

| App en Dokploy | Build Path (contexto) | Dockerfile | Puerto interno | Se expone |
|---|---|---|---|---|
| `smartank-api` | `backend` | `src/Smartank.Api/Dockerfile` | 8080 | No (solo red interna) |
| `smartank-worker` | `backend` | `src/Smartank.Worker/Dockerfile` | sin puerto HTTP | No |
| `smartank-frontend` | `frontend` | `Dockerfile` | 8080 | Si, con el dominio publico |

El `docker-compose.yml` de la raiz existe para desarrollo/QA local (`docker compose up`); **no se usa en Dokploy**: al no correr como
Compose, Dokploy no traduce nombres de variables ni arma URLs compuestas. Cada app recibe, en su pestaña **Environment**, el nombre
final y real de cada variable, tal como la lee el proceso dentro del contenedor.

> Los nombres exactos de los campos (Build Path, Docker File, Docker Context) pueden variar entre versiones de Dokploy; los de
> esta guia corresponden a la v0.29.x. Verifica en la pantalla de cada app que el contexto de build sea la carpeta indicada.

## 1. Orden de creacion

1. **Base de datos.** Crea la base en el SQL Server 2019 y ejecuta una vez `Database/ddl.sql` (y, opcionalmente,
   `Database/seed_estaciones.sql`). Ver [Database/README.md](Database/README.md). Crea un login de aplicacion que lea y escriba filas
   y **no tenga permisos de DDL ni `UPDATE`/`DELETE` sobre `tblAuditLog`**.
2. **`smartank-api`.** Crea la app, carga el bloque de `.env.dokploy` correspondiente y despliega. Anota su hostname interno
   (pestaña Domains/Advanced).
3. **`smartank-worker`.** Mismas conexiones a base y legado que el API, mas Geotab y los parametros `Sync__*`.
4. **`smartank-frontend`.** Define `API_UPSTREAM=http://<hostname-interno-del-api>:8080`, asigna el dominio publico a esta app
   (puerto 8080) y despliega.

Un desfase breve de arranque no es critico: el frontend responde 502 en `/api` hasta que el API este arriba.

## 2. Variables de entorno

`.env.dokploy` trae, agrupadas por app, las variables exactas de cada pestaña **Environment**. Es una plantilla: los secretos y hosts
van como `<...>`. Puntos clave:

- **Bloquean el arranque en Production si faltan o son invalidas:** `ConnectionStrings__Smartank`, `Jwt__Secret`, `Bitacora__Secret`,
  `Servicio__Secreto`, `Cors__OrigenesPermitidos`. Tambien falla el arranque si `Whatsapp__ModoPrueba=true` en Production.
- `Cookie__Secure=true` en produccion. Con `false` y HTTPS el navegador acepta la cookie, pero con `true` y HTTP plano la descarta y el
  login "no entra" sin mostrar error.
- `Database__Esquema` debe coincidir con el esquema donde se ejecuto `ddl.sql` (`dbo` por defecto). No hay migraciones de EF Core: el
  esquema nunca se crea ni se altera desde la aplicacion.
- `ConnectionStrings__Fpsap` y `ConnectionStrings__Ebs` son de **solo lectura** y opcionales: sin ellas el sistema arranca y las
  funciones que las usan responden 503. El data warehouse antiguo solo negocia TLS 1.0; los Dockerfiles ya incluyen la configuracion de
  OpenSSL necesaria (`OPENSSL_CONF`), por lo que no hay que tocar nada en Dokploy.
- `API_UPSTREAM` (frontend) necesita el **hostname interno que Dokploy asigna a `smartank-api`**, no `localhost` ni un nombre de servicio
  de compose.
- Los secretos solo se escriben en Dokploy, nunca en el repositorio. Ver tambien [.env.githubSECRETS](.env.githubSECRETS) para los
  secrets del CI/CD en GitHub.

## 3. CI/CD con GitHub Actions

`.github/workflows/ci-cd-backend.yml` y `ci-cd-frontend.yml` compilan, ejecutan las pruebas y, solo en push a `main` o `DEPLOY` y con
las pruebas en verde, disparan el webhook de despliegue de Dokploy (Dokploy > app > Deployments > Webhook) desde un runner
self-hosted. Detalle de secrets y de la base de pruebas en [.env.githubSECRETS](.env.githubSECRETS).

## 4. Administrador inicial

La primera cuenta se crea con un comando de un solo uso (no hay datos de demostracion en produccion). Con las variables
`Admin__Email`, `Admin__Password` y `Admin__Nombre` definidas en `smartank-api`, ejecuta en el contenedor del API:

```
dotnet Smartank.Api.dll crear-admin
```

Luego **retira** `Admin__Password` del Environment.

## 5. Verificacion posterior al despliegue

- `smartank-api`: `GET /health` -> 200 con el detalle por chequeo (base de datos). Desde el frontend: `https://<dominio>/health`.
- `smartank-frontend`: `https://<dominio>/` carga la SPA y `https://<dominio>/api/auth/me` responde 401 sin sesion (prueba el proxy).
- `smartank-worker`: no tiene health check; revisa los logs: cada grupo de sincronizacion registra su inicio y fin. Si
  `ConnectionStrings__Fpsap` no esta definida veras el aviso de funcion deshabilitada, no un error.
- Inicia sesion con el administrador y abre el tablero.

## 6. Esquema de base de datos

El esquema se crea ejecutando `Database/ddl.sql` completo, de una sola pasada, contra una base vacia (DBeaver, SSMS o `sqlcmd`).
Un cambio de estructura se hace editando ese archivo y aplicando a mano el `ALTER` equivalente en cada base que ya exista. Detalle
en [Database/README.md](Database/README.md) y [docs/MODELO_DE_DATOS.md](docs/MODELO_DE_DATOS.md).
