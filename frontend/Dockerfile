# syntax=docker/dockerfile:1

# ── Etapa 1: compilación de producción ────────────────────────────────────────
# Node 24 cumple los engines de Angular 22 (^22.22.3 || ^24.15.0).
FROM node:24-alpine AS build
WORKDIR /app

# Primero solo los manifiestos: la capa de dependencias se reutiliza mientras no cambien.
COPY package.json package-lock.json ./
RUN npm ci --no-audit --no-fund

COPY . .
RUN npm run build -- --configuration production

# ── Etapa 2: nginx sin privilegios sirviendo el build ─────────────────────────
FROM nginxinc/nginx-unprivileged:1.31-alpine AS runtime

# API_UPSTREAM: origen del API (esquema + host + puerto, sin ruta final). En Dokploy es el
#               nombre interno que recibe la app del API, p. ej. http://smartank-api:8080.
# DNS_RESOLVER: DNS con el que nginx resuelve API_UPSTREAM (127.0.0.11 = DNS de Docker).
# NGINX_ENVSUBST_FILTER restringe envsubst a esas dos variables para no romper los `$`
# propios de nginx dentro de la plantilla.
ENV API_UPSTREAM=http://smartank-api:8080 \
    DNS_RESOLVER=127.0.0.11 \
    NGINX_ENVSUBST_FILTER='^(API_UPSTREAM|DNS_RESOLVER)$'

COPY docker/nginx/default.conf.template /etc/nginx/templates/default.conf.template
COPY docker/nginx/security-headers.conf /etc/nginx/snippets/security-headers.conf
COPY --from=build /app/dist/smartank-frontend/browser /usr/share/nginx/html

# Escucha en 8080 con el usuario `nginx` (uid 101), sin privilegios.
EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=3s --start-period=10s --retries=3 \
  CMD wget -q -O /dev/null http://127.0.0.1:8080/nginx-health || exit 1
