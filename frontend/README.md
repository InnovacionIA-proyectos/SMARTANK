# SMARTANK Frontend

Aplicación web de SMARTANK (gobierno del tanqueo de extremo a extremo), construida con Angular 22. Consume el API de SMARTANK (ASP.NET) mediante rutas relativas `/api/...` y `/health`.

## Requisitos

- Node.js `^22.22.3` o `^24.15.0` (se probó con 24.22.3) y npm 11.
- API de SMARTANK en `http://localhost:8080` para desarrollo (ver `proxy.conf.json`).
- Docker, solo para construir la imagen de producción.

## Comandos

| Comando | Descripción |
|---|---|
| `npm ci` | Instala dependencias exactas del `package-lock.json`. |
| `npm start` | Servidor de desarrollo en `http://localhost:4200`; redirige `/api` y `/health` al API en `localhost:8080`. |
| `npm run build` | Compilación de producción en `dist/smartank-frontend/browser`. |
| `npm test` | Pruebas unitarias (Vitest, una sola pasada). `npm run test:watch` para modo interactivo. |
| `npm run lint` | ESLint con `angular-eslint`. |
| `npm run format` / `format:check` | Prettier. |

## Stack

- Angular 22.2 (componentes standalone, signals, control de cambios `OnPush`), TypeScript 6, RxJS 7.
- Tailwind CSS 3.4 con los tokens del design system (modo claro y oscuro por clase `.dark`).
- Leaflet 1.9 directo (sin wrappers) para los mapas; Chart.js 4 directo para el gráfico de utilización.
- `@tanstack/angular-table` 8.21 para la tabla densa (peer `@angular/core >=17`).
- Íconos Lucide (`lucide`, sin dependencia de Angular) y tipografía Inter local (`@fontsource-variable/inter`).

## Estructura

```
src/app/
  core/
    api/         tipos del API (snake_case), ApiError, construcción de query params
    auth/        AuthService (signals), guards funcionales, roles por módulo
    http/        interceptor funcional (credenciales, errores, 401)
    servicios/   un servicio HttpClient por dominio (viajes, autorizaciones, alertas, ...)
    theme/       tema claro/oscuro
  shared/
    ui/          badge, button, card, data-table, input, select, modal, skeleton, spinner, estados
    mapa/        mapas Leaflet (ruta, recomendación, tablero)
    utils/       fecha, geo, viaje, formato, cargarApi (estado de carga reactivo)
  layout/        shell autenticado: sidebar por rol, menú de usuario, selector de tema
  features/      login, despacho, recomendacion, seguimiento, alertas, historial, tablero,
                 bitacora (pública), no-encontrado
docker/nginx/    plantilla de nginx y cabeceras de seguridad
```

## Rutas

| Ruta | Pantalla | Acceso |
|---|---|---|
| `/login` | Inicio de sesión | Público |
| `/bitacora/:token` | Bitácora móvil del conductor | Público (el token firmado del enlace de WhatsApp es la credencial) |
| `/despacho`, `/recomendacion?id=`, `/seguimiento`, `/historial` | Operación | Sesión |
| `/alertas` | Bandeja de alertas | `control_flota`, `admin` |
| `/tablero` | Recuperación de margen | `finanzas`, `admin` |
| `/` | Redirige a `/despacho` | |
| `**` | 404 | |

La sesión es una cookie httpOnly (`smartank_session`) que el navegador envía sola. La SPA restaura el perfil con `GET /api/auth/me`; un `401` limpia la sesión y navega a `/login` (salvo en las peticiones de la bitácora con token).

## Variables de entorno (contenedor)

El frontend no tiene configuración en tiempo de compilación: usa siempre URLs relativas y nginx reenvía al API.

| Variable | Por defecto | Descripción |
|---|---|---|
| `API_UPSTREAM` | `http://smartank-api:8080` | Origen del API (esquema, host y puerto, sin ruta final). En Dokploy es el nombre interno de la app del API. |
| `DNS_RESOLVER` | `127.0.0.11` | DNS con el que nginx resuelve `API_UPSTREAM` (DNS interno de Docker). |

## Imagen Docker

```bash
docker build -t smartank-frontend:test frontend
docker run --rm -p 8080:8080 -e API_UPSTREAM=http://host.docker.internal:8080 smartank-frontend:test
```

- Etapa 1 `node:24-alpine`: `npm ci` y build de producción.
- Etapa 2 `nginxinc/nginx-unprivileged`: usuario sin privilegios, escucha en el puerto 8080.
- nginx: fallback SPA (`try_files $uri /index.html`), `proxy_pass` de `/api/` y `/health` hacia `API_UPSTREAM` con cabeceras `X-Forwarded-*`, `client_max_body_size 8m` (fotos de hasta 5 MB), gzip, caché inmutable para archivos con hash, `no-cache` para `index.html`, y cabeceras de seguridad (CSP compatible con Leaflet y teselas de OpenStreetMap, `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`, `Permissions-Policy`).
- `GET /nginx-health` responde sin depender del API (usado por el `HEALTHCHECK`).
- La plantilla usa el mecanismo `/etc/nginx/templates/*.template` de la imagen oficial; `NGINX_ENVSUBST_FILTER` restringe `envsubst` a `API_UPSTREAM` y `DNS_RESOLVER`.
- La CSP permite `connect-src` hacia `router.project-osrm.org` (trazado de rutas por carretera). Si se autoaloja otro servicio de teselas o de rutas, ajustar `docker/nginx/security-headers.conf`.

## Despliegue en Dokploy

1. Crear una aplicación de tipo **Dockerfile** apuntando al repositorio.
2. Contexto de compilación: `frontend`. Ruta del Dockerfile: `frontend/Dockerfile` (o `Dockerfile` si el contexto ya es `frontend`).
3. Variable de entorno `API_UPSTREAM` con el hostname interno de la app del API, por ejemplo `http://smartank-api:8080`.
4. Puerto del contenedor: `8080`. Asignar el dominio público a esta aplicación; el API queda accesible solo a través de `/api/`, por lo que la cookie de sesión es del mismo origen.
5. TLS y HSTS los gestiona el proxy de Dokploy.

## Notas de diseño

- Las propiedades del API se usan tal cual llegan (snake_case); no se transforman a camelCase.
- Los componentes de carga de datos usan `cargarApi`, que conserva el último dato mientras recarga para evitar parpadeos.
- Leaflet y Chart.js se cargan bajo demanda (`@defer` y `import()`), fuera del paquete inicial.
- En la bitácora, la vista previa de la foto es local (blob) porque `GET /api/uploads/{id}` exige sesión y el conductor es anónimo.
