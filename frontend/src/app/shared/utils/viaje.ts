import type { EstadoViaje, ViajeApi } from '../../core/api/api-types';
import { progresoRuta } from './geo';

/** Autonomía a monitorear: la actual del vehículo, o la estimada del viaje. */
export function autonomiaDe(
  v: Pick<ViajeApi, 'autonomia_km' | 'autonomia_estimada_km'>,
): number | null {
  return v.autonomia_km ?? v.autonomia_estimada_km;
}

/**
 * Fracción [0..1] de la ruta ya recorrida por el vehículo (proyección
 * origen→destino). 0 si faltan coordenadas (se asume ruta completa por delante).
 */
export function pctRecorridoViaje(v: ViajeApi): number {
  const { origen_lat, origen_lng, destino_lat, destino_lng, vehiculo_lat, vehiculo_lng } = v;
  if (
    origen_lat == null ||
    origen_lng == null ||
    destino_lat == null ||
    destino_lng == null ||
    vehiculo_lat == null ||
    vehiculo_lng == null
  )
    return 0;
  return progresoRuta(
    [origen_lat, origen_lng],
    [destino_lat, destino_lng],
    [vehiculo_lat, vehiculo_lng],
  );
}

/**
 * ¿El viaje necesita tanqueo? Compara la ruta RESTANTE (km de la ruta descontando
 * el recorrido) contra la autonomía actual del vehículo:
 *   · true  (SÍ) → ruta restante > autonomía
 *   · false (NO) → ruta restante ≤ autonomía
 *   · null       → no se puede determinar (sin km de ruta o sin autonomía)
 */
export function necesitaTanqueo(v: ViajeApi): boolean | null {
  const autonomia = autonomiaDe(v);
  if (autonomia == null || v.ruta_km == null) return null;
  const restante = v.ruta_km * (1 - pctRecorridoViaje(v));
  return restante > autonomia;
}

/**
 * Porcentaje [0..100] recorrido para mostrar en tablas, o null si el viaje no trae
 * coordenadas de origen, destino o vehículo (la UI muestra '—').
 */
export function pctRecorridoMostrable(v: ViajeApi): number | null {
  const { origen_lat, origen_lng, destino_lat, destino_lng, vehiculo_lat, vehiculo_lng } = v;
  if (
    origen_lat == null ||
    origen_lng == null ||
    destino_lat == null ||
    destino_lng == null ||
    vehiculo_lat == null ||
    vehiculo_lng == null
  )
    return null;
  return Math.round(pctRecorridoViaje(v) * 100);
}

/**
 * Nivel del tanque como % de la capacidad, o null si falta telemetría de nivel o
 * la capacidad del vehículo.
 */
export function pctNivelTanque(
  v: Pick<ViajeApi, 'nivel_tanque_actual' | 'capacidad_tanque_gal'>,
): number | null {
  const { nivel_tanque_actual: nivel, capacidad_tanque_gal: cap } = v;
  if (nivel == null || !cap) return null;
  return Math.round((nivel / cap) * 100);
}

/** Galones estimados en el tanque (capacidad × nivel%), o null sin telemetría. */
export function galonesEstimados(
  v: Pick<ViajeApi, 'nivel_tanque_actual' | 'capacidad_tanque_gal'>,
): number | null {
  const { nivel_tanque_actual: nivel, capacidad_tanque_gal: cap } = v;
  if (nivel == null || !cap) return null;
  return Math.round((nivel / cap) * cap); // = capacidad × nivel%
}

/** Semáforo de autonomía (km): rojo <100, amarillo 100–200, verde >200. */
export type VarianteSemaforo = 'neutral' | 'danger' | 'warning' | 'success';
export function semaforoAutonomia(km: number | null): { variant: VarianteSemaforo; texto: string } {
  if (km == null) return { variant: 'neutral', texto: '—' };
  const txt = km.toLocaleString('es-CO', { maximumFractionDigits: 0 });
  if (km < 100) return { variant: 'danger', texto: txt };
  if (km <= 200) return { variant: 'warning', texto: txt };
  return { variant: 'success', texto: txt };
}

/** Variante del badge por estado del viaje. */
export const ESTADO_VIAJE_BADGE: Record<EstadoViaje, 'neutral' | 'info' | 'success' | 'danger'> = {
  programado: 'neutral',
  en_ruta: 'info',
  cerrado: 'success',
  descartado: 'danger',
};

/** Despacho y seguimiento solo monitorean viajes activos (programado / en ruta). */
export const ESTADO_VIAJE_OPCIONES = [
  { value: 'programado', label: 'Programado' },
  { value: 'en_ruta', label: 'En ruta' },
];

/** Opciones Sí/No de los filtros "Necesita tanqueo" y "Telemetría actualizada". */
export const SI_NO_OPCIONES = [
  { value: 'si', label: 'Sí' },
  { value: 'no', label: 'No' },
];

/** Fecha efectiva de inicio del viaje: inicio real o, si no, fecha de despacho (FO). */
export function fechaInicioViaje(v: Pick<ViajeApi, 'inicio_at' | 'fecha_despacho'>): Date | null {
  const f = v.inicio_at ?? v.fecha_despacho;
  return f ? new Date(f) : null;
}
