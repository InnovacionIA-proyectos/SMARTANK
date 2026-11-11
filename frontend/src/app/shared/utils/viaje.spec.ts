import type { ViajeApi } from '../../core/api/api-types';
import {
  autonomiaDe,
  fechaInicioViaje,
  galonesEstimados,
  necesitaTanqueo,
  pctNivelTanque,
  pctRecorridoMostrable,
  pctRecorridoViaje,
  semaforoAutonomia,
} from './viaje';

/** Viaje de prueba sobre el ecuador: origen (0,0), destino (0,2) → 222 km aprox. */
function viaje(parcial: Partial<ViajeApi> = {}): ViajeApi {
  return {
    id: 'v1',
    vehiculo_id: 'veh1',
    placa: 'SNU123',
    tipo_motor: 'diesel',
    ruta: 'bogota-medellin',
    nombre_ruta: null,
    codigo_ruta: null,
    estado: 'en_ruta',
    autonomia_estimada_km: null,
    nivel_tanque_actual: null,
    capacidad_tanque_gal: null,
    ubicacion_at: null,
    nivel_at: null,
    autonomia_km: null,
    ruta_km: null,
    vehiculo_lat: 0,
    vehiculo_lng: 1,
    origen_lat: 0,
    origen_lng: 0,
    destino_lat: 0,
    destino_lng: 2,
    manifiesto: null,
    inicio_at: null,
    fecha_despacho: null,
    recomendacion_activa: false,
    ...parcial,
  };
}

describe('pctRecorridoViaje', () => {
  it('proyecta el vehículo sobre origen→destino', () => {
    expect(pctRecorridoViaje(viaje())).toBeCloseTo(0.5, 9);
  });

  it('es 0 si falta cualquier coordenada', () => {
    expect(pctRecorridoViaje(viaje({ vehiculo_lat: null }))).toBe(0);
    expect(pctRecorridoViaje(viaje({ origen_lng: null }))).toBe(0);
    expect(pctRecorridoViaje(viaje({ destino_lat: null }))).toBe(0);
  });
});

describe('pctRecorridoMostrable', () => {
  it('redondea a porcentaje entero', () => {
    expect(pctRecorridoMostrable(viaje({ vehiculo_lng: 0.5 }))).toBe(25);
  });

  it('es null (la UI muestra guion) si faltan coordenadas', () => {
    expect(pctRecorridoMostrable(viaje({ vehiculo_lng: null }))).toBeNull();
  });
});

describe('necesitaTanqueo', () => {
  it('es true cuando la ruta restante supera la autonomía', () => {
    // 50% recorrido de 400 km → 200 km restantes > 150 km de autonomía
    expect(necesitaTanqueo(viaje({ ruta_km: 400, autonomia_km: 150 }))).toBe(true);
  });

  it('es false cuando la autonomía cubre la ruta restante (incluye igualdad)', () => {
    expect(necesitaTanqueo(viaje({ ruta_km: 400, autonomia_km: 250 }))).toBe(false);
    expect(necesitaTanqueo(viaje({ ruta_km: 400, autonomia_km: 200 }))).toBe(false);
  });

  it('usa la autonomía estimada del viaje si no hay la actual del vehículo', () => {
    expect(necesitaTanqueo(viaje({ ruta_km: 400, autonomia_estimada_km: 100 }))).toBe(true);
  });

  it('es null si no se puede determinar', () => {
    expect(necesitaTanqueo(viaje({ ruta_km: null, autonomia_km: 100 }))).toBeNull();
    expect(necesitaTanqueo(viaje({ ruta_km: 400 }))).toBeNull();
  });

  it('sin coordenadas asume la ruta completa por delante', () => {
    expect(necesitaTanqueo(viaje({ ruta_km: 400, autonomia_km: 300, vehiculo_lat: null }))).toBe(
      true,
    );
  });
});

describe('autonomiaDe', () => {
  it('prefiere la autonomía actual y cae a la estimada', () => {
    expect(autonomiaDe({ autonomia_km: 120, autonomia_estimada_km: 80 })).toBe(120);
    expect(autonomiaDe({ autonomia_km: null, autonomia_estimada_km: 80 })).toBe(80);
    expect(autonomiaDe({ autonomia_km: null, autonomia_estimada_km: null })).toBeNull();
  });
});

describe('nivel del tanque', () => {
  it('calcula porcentaje y galones estimados', () => {
    const v = { nivel_tanque_actual: 75, capacidad_tanque_gal: 300 };
    expect(pctNivelTanque(v)).toBe(25);
    expect(galonesEstimados(v)).toBe(75);
  });

  it('devuelve null sin telemetría o sin capacidad', () => {
    expect(pctNivelTanque({ nivel_tanque_actual: null, capacidad_tanque_gal: 300 })).toBeNull();
    expect(pctNivelTanque({ nivel_tanque_actual: 10, capacidad_tanque_gal: 0 })).toBeNull();
    expect(galonesEstimados({ nivel_tanque_actual: 10, capacidad_tanque_gal: null })).toBeNull();
  });
});

describe('semaforoAutonomia', () => {
  it('rojo por debajo de 100 km, amarillo de 100 a 200, verde por encima', () => {
    expect(semaforoAutonomia(99).variant).toBe('danger');
    expect(semaforoAutonomia(100).variant).toBe('warning');
    expect(semaforoAutonomia(200).variant).toBe('warning');
    expect(semaforoAutonomia(201).variant).toBe('success');
  });

  it('sin dato es neutral con guion', () => {
    expect(semaforoAutonomia(null)).toEqual({ variant: 'neutral', texto: '—' });
  });
});

describe('fechaInicioViaje', () => {
  it('usa el inicio real y, si no hay, la fecha de despacho', () => {
    expect(
      fechaInicioViaje({
        inicio_at: '2026-01-02T00:00:00Z',
        fecha_despacho: '2026-01-01T00:00:00Z',
      })?.toISOString(),
    ).toBe('2026-01-02T00:00:00.000Z');
    expect(
      fechaInicioViaje({ inicio_at: null, fecha_despacho: '2026-01-01T00:00:00Z' })?.toISOString(),
    ).toBe('2026-01-01T00:00:00.000Z');
    expect(fechaInicioViaje({ inicio_at: null, fecha_despacho: null })).toBeNull();
  });
});
