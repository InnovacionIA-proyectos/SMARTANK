/**
 * Contrato JSON del API (snake_case, enums en texto snake_case). Las propiedades
 * se usan tal cual llegan del backend: no se transforman a camelCase.
 */

// ── Enums ────────────────────────────────────────────────────────────────────
export type Rol = 'despacho' | 'control_flota' | 'finanzas' | 'admin';

export type EstadoViaje = 'programado' | 'en_ruta' | 'cerrado' | 'descartado';

export type EstadoAutorizacion =
  'propuesta' | 'autorizada' | 'ejecutada' | 'ejecutada_parcial' | 'expirada' | 'rechazada';

export type TipoAlerta =
  'desviacion_consumo_carga' | 'desviacion_consumo' | 'estacion_cara' | 'ejecucion_parcial';

export type EstadoAlerta = 'nueva' | 'en_revision' | 'gestionada' | 'descartada';

export type FuenteRuta = 'valhalla' | 'dane';
export type FuenteTanqueo = 'distracom' | 'terpel';

/** Par [lat, lng] de un punto de la polilínea de ruta. */
export type PuntoRuta = [number, number];

// ── Errores ──────────────────────────────────────────────────────────────────
/** Cuerpo de error del API. */
export interface ErrorApiCuerpo {
  code: string;
  message: string;
  timestamp: string;
  details?: unknown;
}

// ── Autenticación ────────────────────────────────────────────────────────────
export interface UsuarioSesion {
  id: string;
  email: string;
  nombre: string | null;
  rol: Rol;
}
export interface LoginRequest {
  email: string;
  password: string;
}
/** POST /api/auth/login */
export interface LoginResponse {
  usuario: UsuarioSesion;
}
/** GET /api/auth/me */
export type MeResponse = UsuarioSesion;

// ── Viajes ───────────────────────────────────────────────────────────────────
export interface ViajeApi {
  id: string;
  vehiculo_id: string;
  placa: string;
  tipo_motor: string;
  ruta: string;
  nombre_ruta: string | null; // NOMBRERUTA del FO (legible)
  codigo_ruta: string | null; // CODIGORUTA del FO
  estado: EstadoViaje;
  autonomia_estimada_km: number | null;
  nivel_tanque_actual: number | null; // nivel actual del tanque (galones, telemetría)
  capacidad_tanque_gal: number | null; // capacidad total del tanque (galones)
  ubicacion_at: string | null; // fecha/hora de la última telemetría de posición
  nivel_at: string | null; // fecha/hora de la última lectura de nivel (Geotab)
  autonomia_km: number | null; // autonomía actual del vehículo (km)
  ruta_km: number | null; // longitud total de la ruta del viaje (km)
  vehiculo_lat: number | null;
  vehiculo_lng: number | null;
  origen_lat: number | null;
  origen_lng: number | null;
  destino_lat: number | null;
  destino_lng: number | null;
  manifiesto: string | null;
  inicio_at: string | null; // inicio real (en_ruta/cerrado); null en programado
  fecha_despacho: string | null; // fecha de despacho programada (FO)
  recomendacion_activa: boolean; // autorización de tanqueo vigente (propuesta/autorizada)
}

/** GET /api/viajes */
export interface ViajesResponse {
  total: number;
  viajes: ViajeApi[];
  opciones: { rutas: string[]; anios: number[] };
}

/** Query de GET /api/viajes (todos opcionales, se aplican en el servidor). */
export interface ViajesFiltros {
  estado?: string;
  ruta?: string;
  placa?: string;
  manifiesto?: string;
  anio?: string;
  mes?: string;
  desplazamiento?: string; // 'vacio' | 'cargado'
  telemetria?: string; // 'si' | 'no'
  con_recomendacion?: string; // '1'
}

/** GET /api/viajes/{id}/ruta */
export interface RutaViajeApi {
  viaje: { id: string; ruta: string; estado: EstadoViaje; placa: string };
  ruta: { polilinea: PuntoRuta[]; kilometros: number | null; num_puntos: number | null } | null;
  vehiculo: { placa: string; lat: number; lng: number; ubicacion_at: string | null } | null;
  autonomia_km: number | null;
}

/** POST /api/viajes/{id}/autonomia */
export interface AutonomiaResponse {
  viaje: Record<string, unknown>;
  autonomia_km: number;
  base_rendimiento: 'historico' | 'esperado' | 'default';
  rendimiento_usado: number;
  tolerancia: number;
}

/** POST /api/viajes/{id}/recomendacion (body opcional) */
export interface RecomendacionRequest {
  galones?: number;
}
export interface RecomendacionResponse {
  requiere_tanqueo: boolean;
  autonomia_km: number;
  ruta_km_restante: number | null;
  motivo: string | null;
  autorizacion: AutorizacionPublica | null;
  estacion_elegida: EstacionPublica | null;
  galones_max: number | null;
  precio_techo: number | null;
  delta_ahorro_estimado: number | null;
  estaciones_candidatas?: unknown;
  sobre_ruta?: unknown;
}

// ── Autorizaciones ───────────────────────────────────────────────────────────
/** Autorización tal como la serializa el API (POST/acciones). */
export interface AutorizacionPublica {
  id: string;
  viaje_id: string;
  vehiculo_id: string;
  estacion_id: string;
  galones_max: number;
  precio_techo: number;
  delta_ahorro_estimado: number | null;
  fecha_inicio: string;
  fecha_fin: string;
  estado: EstadoAutorizacion;
  autorizado_por: string | null;
  eds_confirmacion: Record<string, unknown> | null;
  created_at: string;
  updated_at: string;
}

/** POST /api/autorizaciones */
export interface AutorizacionCreateRequest {
  viaje_id: string;
  vehiculo_id: string;
  estacion_id: string;
  galones_max: number;
  precio_techo: number;
  delta_ahorro_estimado?: number | null;
  fecha_inicio: string;
  fecha_fin: string;
}

/** GET /api/autorizaciones/{id} */
export interface AutorizacionDetalleApi {
  id: string;
  viaje_id: string;
  placa: string;
  manifiesto: string | null;
  viaje_ruta: string | null;
  viaje_nombre_ruta: string | null;
  viaje_codigo_ruta: string | null;
  programa: string | null;
  cedula_conductor: string | null;
  conductor_nombre: string | null;
  estacion_id: string;
  estacion_nombre: string;
  estacion_ruta: string | null;
  estacion_lat: number | null;
  estacion_lng: number | null;
  vehiculo_lat: number | null;
  vehiculo_lng: number | null;
  vehiculo_ubicacion_at: string | null;
  vehiculo_nivel_at: string | null; // fecha/hora de la última lectura de nivel (Geotab)
  nivel_tanque_actual: number | null;
  capacidad_tanque_gal: number | null;
  autonomia_km: number | null;
  ruta_km: number | null;
  origen_lat: number | null;
  origen_lng: number | null;
  destino_lat: number | null;
  destino_lng: number | null;
  galones_max: number;
  precio_techo: number;
  delta_ahorro_estimado: number | null;
  estado: EstadoAutorizacion;
  fecha_inicio: string;
  fecha_fin: string;
}

/** POST /api/autorizaciones/{id}/notificar */
export interface NotificarResponse {
  autorizacion_id?: string;
  bitacora_url: string;
  destinatario: { nombre: string; telefono: string };
  orden?: {
    placa: string | null;
    manifiesto: string | null;
    ruta: string | null;
    estacion: string;
    galones_autorizados: number;
    mapa_url?: string;
  };
  whatsapp: { enviado: boolean; sid?: string; motivo?: string };
}

/** POST /api/autorizaciones/{id}/preautorizar-eds */
export interface PreautorizarEdsResponse {
  autorizacion: AutorizacionPublica;
  eds: Record<string, unknown> & { simulado?: boolean; ok?: boolean; referencia?: string };
}

// ── Estaciones ───────────────────────────────────────────────────────────────
export interface EstacionApi {
  id: string;
  nombre: string;
  ruta: string | null;
  lat: number | null;
  lng: number | null;
  precio_galon_actual: number | null;
  precio_acpm: number | null;
  precio_corriente: number | null;
  proveedor: string | null;
  producto: string | null;
}
/** Estación completa tal como la serializa el API. */
export interface EstacionPublica extends EstacionApi {
  precio_actualizado_at: string | null;
  soporta_preautorizacion: boolean;
  codigo_externo: string | null;
  created_at: string;
  updated_at: string;
}
/** GET /api/estaciones?ruta= */
export interface EstacionesResponse {
  ruta?: string | null;
  total: number;
  estaciones: EstacionApi[];
}

/** POST /api/estaciones/precios/sync (solo admin; sin body = modo automático). */
export interface SyncPreciosRequest {
  ruta?: string;
  precios?: { estacion_id: string; precio_galon_actual: number }[];
}
export interface SyncPreciosResponse {
  modo: 'manual' | 'automatico';
  actualizadas?: number;
  estaciones?: EstacionPublica[];
  fuente?: Record<string, { total: number; actualizadas: number; creadas: number }>;
}

// ── Alertas ──────────────────────────────────────────────────────────────────
export interface AlertaApi {
  id: string;
  tipo: TipoAlerta;
  placa: string;
  magnitud_desviacion: number | null;
  estado: EstadoAlerta;
  created_at: string;
}
/** GET /api/alertas?estado= */
export interface AlertasResponse {
  estado?: EstadoAlerta | null;
  total: number;
  alertas: AlertaApi[];
}
/** PATCH /api/alertas/{id} */
export interface AlertaGestionRequest {
  estado: EstadoAlerta;
}
/** Alerta tal como la serializa el API (PATCH / POST de servicio). */
export interface AlertaPublica {
  id: string;
  tipo: TipoAlerta;
  vehiculo_id: string;
  evento_id: string | null;
  magnitud_desviacion: number | null;
  estado: EstadoAlerta;
  gestionada_por: string | null;
  created_at: string;
  updated_at: string;
}
/** POST /api/alertas (job de servicio; no lo consume la SPA). */
export interface AlertaCreateRequest {
  tipo: TipoAlerta;
  vehiculo_id: string;
  evento_id?: string | null;
  magnitud_desviacion?: number | null;
}

// ── Eventos / historial ──────────────────────────────────────────────────────
export interface EventoTanqueoApi {
  id: string;
  placa: string;
  tipo_motor: string;
  programa: string | null; // programa del vehículo al registrar el tanqueo
  estacion_nombre: string;
  estacion_ruta: string | null;
  proveedor: string | null; // programa/red de la EDS: 'terpel' | 'distracom' | null
  galones_cargados: number;
  precio_pagado: number | null;
  odometro_evento: number | null;
  autorizado: boolean; // ¿casa con una autorización de la app (placa+estación+fecha)?
  precio_techo: number | null; // techo autorizado, si está enlazado
  // Datos cargados por el conductor / telemetría (bitácora del tanqueo).
  foto_surtidor_url: string | null;
  nivel_antes: number | null;
  nivel_despues: number | null;
  ocurrido_at: string;
}
/** GET /api/eventos */
export interface EventosResponse {
  total: number;
  eventos: EventoTanqueoApi[];
}
/** Query de GET /api/eventos (fechas en YYYY-MM-DD, rango inclusivo). */
export interface EventosFiltros {
  placa?: string;
  proveedor?: string;
  desde?: string;
  hasta?: string;
}

/** POST /api/eventos (anónimo con token de bitácora). */
export interface EventoCreateRequest {
  token: string;
  galones_cargados: number;
  precio_pagado?: number | null;
  foto_surtidor_url?: string | null;
  odometro_evento?: number | null;
  nivel_antes?: number | null;
  nivel_despues?: number | null;
}
/** Evento tal como lo serializa el API (respuesta 201 de POST /api/eventos). */
export interface EventoPublico {
  id: string;
  autorizacion_id: string;
  vehiculo_id: string;
  estacion_id: string;
  galones_cargados: number;
  precio_pagado: number | null;
  foto_surtidor_url: string | null;
  odometro_evento: number | null;
  nivel_antes: number | null;
  nivel_despues: number | null;
  registrado_por: string | null;
  programa: string | null;
  producto: string | null;
  ocurrido_at: string;
  created_at: string;
  updated_at: string;
}
/** POST /api/eventos/{id}/verificar (job de servicio; no lo consume la SPA). */
export interface VerificarEventoResponse {
  evento_id: string;
  estado_autorizacion: 'ejecutada' | 'ejecutada_parcial';
  alertas_generadas: number;
  alertas: AlertaPublica[];
}

// ── Bitácora del conductor (pública, con token) ──────────────────────────────
/** GET /api/bitacora/{token} */
export interface BitacoraOrdenApi {
  autorizacion_id: string;
  placa: string;
  manifiesto: string | null;
  ruta: string | null;
  estacion_nombre: string;
  galones_max: number;
  estado: EstadoAutorizacion;
  ya_registrada: boolean;
  vence_at: string;
}
/** POST /api/uploads?token={token} */
export interface UploadResponse {
  id: string;
  url: string;
}

// ── Métricas ─────────────────────────────────────────────────────────────────
export interface RecuperacionFiltrosApi {
  programa: string | null;
  proveedor: string | null;
  producto: string | null;
  anio: number | null;
  mes: number | null;
  ruta: string | null;
}
export interface UtilizacionEdsApi {
  proveedor: string;
  tanqueos: number;
  galones: number;
}
export interface EquipoFilaApi {
  equipo: string;
  mes: number;
  fecha: string; // fecha/hora del tanqueo (ISO)
  valor_galon: number; // valor por galón (o valor_total/galones si no viene)
  galones: number;
  valor_total: number;
  eds: string;
  ciudad: string | null;
}
export interface GalonesPorEdsApi {
  id: string;
  nombre: string;
  lat: number;
  lng: number;
  ruta: string | null;
  proveedor: string | null;
  precio: number | null;
  galones: number;
  tanqueos: number;
}
export interface RutaLineaApi {
  ruta: string | null;
  origen_lat: number;
  origen_lng: number;
  destino_lat: number;
  destino_lng: number;
}
/** GET /api/metricas/recuperacion */
export interface MetricasResponse {
  filtros: RecuperacionFiltrosApi;
  galones_total: number;
  tanqueos_total: number;
  gasto_real: number;
  gasto_ideal: number;
  gasto_maximo: number;
  ahorro_vs_maximo: number;
  oportunidad_vs_ideal: number;
  precio_minimo_ruta: number | null;
  utilizacion_eds: UtilizacionEdsApi[];
  equipos: EquipoFilaApi[];
  galones_por_eds: GalonesPorEdsApi[];
  rutas: RutaLineaApi[];
  opciones: {
    programas: string[];
    proveedores: string[];
    productos: string[];
    anios: number[];
    meses: number[];
    rutas: string[];
  };
}
/** Query de GET /api/metricas/recuperacion. */
export interface MetricasFiltros {
  programa?: string;
  proveedor?: string;
  producto?: string;
  anio?: string;
  mes?: string;
  ruta?: string;
}

// ── Vehículos / telemetría ───────────────────────────────────────────────────
/** GET /api/vehiculos/{id}/telemetria */
export interface VehiculoTelemetriaResponse {
  vehiculo_id: string;
  placa: string;
  nivel_tanque_actual: number | null;
  odometro: number | null;
  tiene_telemetria: boolean;
  actualizado_at: string;
}
/** POST /api/telemetria/ingesta (cabecera X-Service-Secret; no lo consume la SPA). */
export interface TelemetriaIngestaRequest {
  placa: string;
  nivel_galones: number;
  odometro?: number;
  lat?: number;
  lng?: number;
  leido_at?: string;
}
