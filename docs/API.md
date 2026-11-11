# API de SMARTANK

Referencia de los endpoints REST. Con `ASPNETCORE_ENVIRONMENT=Development` el contrato completo y navegable esta en `/swagger`.

## 1. Convenciones

| Aspecto | Regla |
|---|---|
| Base | Todo bajo `/api`, mas `GET /health` |
| Formato | JSON UTF-8. Propiedades en **snake_case** (`galones_max`); enums como texto snake_case (`en_ruta`, `ejecutada_parcial`, `control_flota`) |
| Fechas | ISO 8601 con zona (`2026-11-11T14:30:00+00:00`) |
| Numeros | Dinero y galones como decimal; kilometros, rendimientos y coordenadas como numero de coma flotante |
| Polilineas | `[[lat, lng], ...]` |
| Sesion | Cookie `smartank_session` (JWT, `HttpOnly`, `SameSite=Strict`, `Secure` segun `Cookie__Secure`). Se fija en el login; no se usa el encabezado `Authorization` |
| Servicio a servicio | Encabezado `X-Service-Secret` (secreto `Servicio__Secreto`) |
| Conductor | Sin cuenta: token firmado de la bitacora (parametro de ruta o de consulta o campo del cuerpo) |

### Formato de error

Toda respuesta de error tiene esta forma:

```json
{ "code": "AUTORIZACION_NO_AUTORIZADA", "message": "La autorizacion debe estar autorizada.", "timestamp": "2026-11-11T14:30:00+00:00", "details": null }
```

`details` lleva, por ejemplo, el detalle por campo en errores de validacion (`VALIDATION_ERROR`, `TELEMETRIA_INVALIDA`).

| HTTP | Cuando |
|---|---|
| 400 | Dato invalido (`VALIDATION_ERROR` y codigos propios) |
| 401 | Sin sesion o sesion vencida (`NO_AUTENTICADO`, `SESION_EXPIRADA`), credenciales o secreto invalidos |
| 403 | Rol insuficiente (`ACCESO_DENEGADO`) |
| 404 | Recurso inexistente |
| 409 | Conflicto de estado o de concurrencia (`CONFLICTO_CONCURRENCIA`, `REGISTRO_DUPLICADO`, ...) |
| 429 | Limite de solicitudes (`DEMASIADAS_SOLICITUDES`, con `Retry-After`) |
| 503 | Dependencia no configurada (`FPSAP_NO_CONFIGURADO`, `GEOTAB_NO_CONFIGURADO`, ...) |
| 500 | Error inesperado (`INTERNAL_ERROR`; el detalle solo se muestra en Development) |

### Roles

`despacho`, `control_flota`, `finanzas`, `admin`. `admin` puede todo lo que puede cualquier otro rol. "Sesion" significa cualquier rol autenticado.

### Limites de solicitudes (por IP)

| Politica | Limite | Aplica a |
|---|---|---|
| Login | 10 por minuto | `POST /api/auth/login` |
| Escritura publica | 30 por minuto | Endpoints del conductor (bitacora, eventos, uploads) |
| Listados | 300 por minuto | Listados de consulta |

## 2. Endpoints

### Autenticacion

| Metodo y ruta | Acceso | Descripcion |
|---|---|---|
| `POST /api/auth/login` | Anonimo | Body `{ email, password }`. Responde `{ usuario: { id, email, nombre, rol } }` y fija la cookie. 401 `CREDENCIALES_INVALIDAS` (mismo mensaje exista o no el correo) |
| `POST /api/auth/logout` | Anonimo | Borra la cookie. Responde `{ ok: true }` |
| `GET /api/auth/me` | Sesion | Usuario actual `{ id, email, nombre, rol }` (lo usa la SPA para restaurar la sesion) |

### Viajes, rutas y vehiculos

| Metodo y ruta | Acceso | Descripcion |
|---|---|---|
| `GET /api/viajes` | Sesion | Listado con `{ total, viajes[], opciones: { rutas, anios } }`. Filtros: `estado` (`programado`, `en_ruta`, `descartado`), `ruta`, `placa`, `manifiesto`, `anio`, `mes`, `desplazamiento` (`vacio`, `cargado`), `con_recomendacion=1`, `telemetria` (`si`, `no`). Valores no reconocidos se ignoran |
| `GET /api/viajes/{id}/ruta` | Sesion | `{ viaje, ruta: { polilinea, kilometros, num_puntos } o null, vehiculo, autonomia_km }` |
| `POST /api/viajes/{id}/autonomia` | despacho, admin | Calcula y guarda la autonomia: `{ viaje, autonomia_km, base_rendimiento, rendimiento_usado, tolerancia }`. 409 `SIN_TELEMETRIA_NIVEL`, 409 `TELEMETRIA_OBSOLETA` |
| `POST /api/viajes/{id}/recomendacion` | despacho, admin | Body opcional `{ galones }`. Elige la EDS y crea o actualiza la propuesta. Responde `requiere_tanqueo`, `autonomia_km`, `ruta_km_restante`, `motivo`, `autorizacion`, `estacion_elegida`, `galones_max`, `precio_techo`, `delta_ahorro_estimado`, `estaciones_candidatas`, `sobre_ruta` |
| `GET /api/vehiculos/{id}/telemetria` | Sesion | Ultimo nivel de tanque y odometro del vehiculo |
| `POST /api/telemetria/ingesta` | Anonimo + `X-Service-Secret` | Body `{ placa, nivel_galones, odometro?, lat?, lng?, leido_at? }`. 404 `VEHICULO_NO_ENCONTRADO` |

### Estaciones

| Metodo y ruta | Acceso | Descripcion |
|---|---|---|
| `GET /api/estaciones?ruta=` | Sesion | `{ ruta, total, estaciones[] }` con precio por producto |
| `POST /api/estaciones/precios/sync` | admin | Ejecuta la sincronizacion de precios y devuelve el resultado con contadores |

### Autorizaciones de tanqueo

| Metodo y ruta | Acceso | Descripcion |
|---|---|---|
| `GET /api/autorizaciones?estado=&viaje_id=` | Sesion | `{ total, autorizaciones[] }` con detalle (maximo 200) |
| `GET /api/autorizaciones/{id}` | Sesion | Detalle con estacion, vehiculo, conductor, ruta y autonomia |
| `POST /api/autorizaciones` | despacho, admin | Crea una propuesta manual. 409 `PROPUESTA_EXISTENTE` si el viaje ya tiene una |
| `POST /api/autorizaciones/{id}/autorizar` | despacho, admin | `propuesta` -> `autorizada`. 409 `TRANSICION_INVALIDA` |
| `POST /api/autorizaciones/{id}/preautorizar-eds` | despacho, admin | Pre-autoriza en la EDS. Sin `Eds__ApiBaseUrl` responde `eds.simulado = true` |
| `POST /api/autorizaciones/{id}/notificar` | despacho, admin | Envia la orden por WhatsApp con el enlace de bitacora. Responde `{ autorizacion_id, destinatario, orden, bitacora_url, whatsapp: { enviado, sid?, motivo? } }`. Sin WhatsApp configurado la orden se prepara con `enviado = false`. 409 `CONDUCTOR_SIN_CELULAR` |

Estados de la autorizacion: `propuesta`, `autorizada`, `ejecutada`, `ejecutada_parcial`, `expirada`, `rechazada`. Las no ejecutadas se conservan.

### Bitacora del conductor (sin sesion, con token)

El enlace que recibe el conductor es `{App__UrlPublica}/bitacora/{token}`. El token es un JWT firmado, ligado a una autorizacion, con vigencia `Bitacora__ExpiraHoras`.

| Metodo y ruta | Acceso | Descripcion |
|---|---|---|
| `GET /api/bitacora/{token}` | Anonimo | `{ autorizacion_id, placa, manifiesto, ruta, estacion_nombre, galones_max, estado, ya_registrada, vence_at }`. 401 `BITACORA_TOKEN_INVALIDO` o `BITACORA_TOKEN_EXPIRADO` |
| `POST /api/uploads?token=` | Anonimo | Cuerpo = bytes de la imagen (`image/*`, maximo 5 MB; se valida la firma real JPEG, PNG o WEBP). Responde 201 `{ id, url }` |
| `POST /api/eventos` | Anonimo | Body `{ token, galones_cargados, precio_pagado?, foto_surtidor_url?, odometro_evento?, nivel_antes?, nivel_despues? }`. Registra la carga y la verifica de inmediato. Un solo evento por autorizacion: 409 `BITACORA_YA_REGISTRADA`. 409 `AUTORIZACION_NO_AUTORIZADA` o `AUTORIZACION_FUERA_DE_VENTANA` |
| `GET /api/uploads/{id}` | Sesion | Bytes de la imagen (`Cache-Control: private`) |

### Eventos y alertas

| Metodo y ruta | Acceso | Descripcion |
|---|---|---|
| `GET /api/eventos?placa=&proveedor=&desde=&hasta=` | Sesion | Historial combinado de tanqueos (Terpel y Distracom) con la autorizacion y la bitacora cuando se pueden casar |
| `POST /api/eventos/{id}/verificar` | despacho, control_flota, admin | Reverifica un evento. 409 `EVENTO_YA_VERIFICADO` |
| `GET /api/alertas?estado=` | control_flota, admin | `{ estado, total, alertas[] }`. 400 `ESTADO_ALERTA_INVALIDO` |
| `PATCH /api/alertas/{id}` | control_flota, admin | Cambia el estado. Transiciones: `nueva` -> `en_revision`, `gestionada` o `descartada`; `en_revision` -> `gestionada` o `descartada`. 409 `TRANSICION_ALERTA_INVALIDA` |
| `POST /api/alertas` | Anonimo + `X-Service-Secret` | Crea una alerta (uso de servicios) |

Alertas generadas por la verificacion: `desviacion_consumo_carga` (faltante mayor al umbral entre lo cargado y el aumento real de nivel), `ejecucion_parcial` (carga menor a lo autorizado mas alla de la tolerancia) y `estacion_cara` (precio por encima de la EDS mas barata de la ruta).

### Metricas

| Metodo y ruta | Acceso | Descripcion |
|---|---|---|
| `GET /api/metricas/recuperacion` | finanzas, admin | Recuperacion de margen. Filtros: `programa`, `proveedor`, `producto`, `anio`, `mes`, `ruta`. Responde totales (`galones_total`, `gasto_real`, `gasto_ideal`, `gasto_maximo`, `ahorro_vs_maximo`, `oportunidad_vs_ideal`), `utilizacion_eds`, `equipos`, `galones_por_eds`, `rutas` y `opciones` |

### Operacion

| Metodo y ruta | Acceso | Descripcion |
|---|---|---|
| `GET /health` | Anonimo | `{ status, duracion, chequeos }`; 200 si la base responde, 503 si no |

## 3. Diferencias respecto al sistema anterior

- El error usa `message` (antes `error`).
- La bitacora usa un token firmado en lugar del manifiesto, que es predecible. Los enlaces antiguos no funcionan.
- `GET /api/uploads/{id}` ahora requiere sesion.
- `GET /api/auth/me` es nuevo (restaura la sesion de la SPA).
- Recomendar tanqueo no duplica propuestas: actualiza la vigente del viaje.
- `POST /api/eventos` verifica la carga de forma automatica.
- Se rechaza calcular la autonomia con una lectura de nivel mas vieja que `ParametrosNegocio__NivelMaxAntiguedadHoras`.
- No existen `/api/inngest` ni la integracion con Twilio.
