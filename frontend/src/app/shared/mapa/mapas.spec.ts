import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import type { GalonesPorEdsApi, RutaLineaApi } from '../../core/api/api-types';
import { type LatLng } from '../utils/geo';
import { type EstacionMapa, MapaRecomendacion } from './mapa-recomendacion';
import { MapaRuta, type VehiculoMapa } from './mapa-ruta';
import { MapaTablero, colorCosto } from './mapa-tablero';

/** Contexto 2D falso: jsdom no implementa canvas y Leaflet lo usa con `preferCanvas`. */
const contexto2dFalso = new Proxy(
  {},
  {
    get: (_destino, propiedad) => (propiedad === 'canvas' ? undefined : () => undefined),
    set: () => true,
  },
);

// Los tests de Leaflet dibujan cientos de marcadores en jsdom: más holgura que los 5 s por defecto.
vi.setConfig({ testTimeout: 30_000 });

beforeEach(() => {
  // jsdom reporta contenedores de 0 px y Leaflet encuadraría al zoom máximo (miles de flechas).
  vi.spyOn(HTMLElement.prototype, 'clientWidth', 'get').mockReturnValue(800);
  vi.spyOn(HTMLElement.prototype, 'clientHeight', 'get').mockReturnValue(500);
  // Sin red: las rutas por carretera (OSRM) caen al trazo recto original.
  vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new Error('sin red')));
  vi.spyOn(HTMLCanvasElement.prototype, 'getContext').mockReturnValue(
    contexto2dFalso as unknown as CanvasRenderingContext2D,
  );
});
afterEach(() => {
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
});

const LINEA: LatLng[] = [
  [4.7, -74.0],
  [5.2, -74.5],
  [5.8, -75.0],
  [6.2, -75.5],
];
const VEHICULO: VehiculoMapa = {
  placa: 'SNU123',
  lat: 5.25,
  lng: -74.52,
  ubicacion_at: '2026-06-17T10:00:00',
};

async function estabilizar(fixture: { detectChanges(): void; whenStable(): Promise<unknown> }) {
  fixture.detectChanges();
  await fixture.whenStable();
  fixture.detectChanges();
}

describe('MapaRuta', () => {
  @Component({
    imports: [MapaRuta],
    template: `<app-mapa-ruta
      [polilinea]="linea()"
      [vehiculo]="vehiculo()"
      [autonomiaKm]="autonomia()"
      ruta="Bogotá - Medellín"
      [kilometros]="420"
    />`,
  })
  class Host {
    readonly linea = signal<LatLng[]>(LINEA);
    readonly vehiculo = signal<VehiculoMapa | null>(VEHICULO);
    readonly autonomia = signal<number | null>(150);
  }

  it('dibuja el mapa con la ruta, el tramo de autonomía y el vehículo, y se destruye sin errores', async () => {
    const fixture = TestBed.createComponent(Host);
    await estabilizar(fixture);
    const raiz = fixture.nativeElement as HTMLElement;

    expect(raiz.querySelector('.leaflet-container')).not.toBeNull();
    expect(raiz.textContent).toContain('Ruta · 420 km');
    expect(raiz.textContent).toContain('Autonomía · 150 km');
    expect(raiz.querySelectorAll('.leaflet-marker-icon').length).toBeGreaterThan(0);

    fixture.componentInstance.autonomia.set(null);
    await estabilizar(fixture);
    expect(raiz.textContent).toContain('Autonomía · sin dato');

    fixture.componentInstance.vehiculo.set(null);
    await estabilizar(fixture);
    expect(raiz.textContent).toContain('sin telemetría de posición del vehículo');

    fixture.destroy();
  });

  it('sin ruta cargada muestra el aviso en lugar del mapa', async () => {
    const fixture = TestBed.createComponent(Host);
    fixture.componentInstance.linea.set([]);
    await estabilizar(fixture);
    const raiz = fixture.nativeElement as HTMLElement;
    expect(raiz.querySelector('.leaflet-container')).toBeNull();
    expect(raiz.textContent).toContain('Este viaje no tiene ruta cargada.');
  });
});

describe('MapaRecomendacion', () => {
  const ESTACIONES: EstacionMapa[] = [
    {
      id: 'e1',
      nombre: 'EDS Elegida',
      lat: 5.75,
      lng: -74.95,
      precio_galon_actual: 9000,
      precio_acpm: 9000,
      proveedor: 'terpel',
    },
    {
      id: 'e2',
      nombre: 'EDS Cercana',
      lat: 5.76,
      lng: -74.96,
      precio_galon_actual: 9100,
      precio_acpm: 9100,
      proveedor: 'distracom',
    },
    {
      id: 'e3',
      nombre: 'EDS Lejana',
      lat: 6.2,
      lng: -75.5,
      precio_galon_actual: 9200,
      precio_acpm: 9200,
    },
    {
      id: 'e4',
      nombre: 'EDS Sin ACPM',
      lat: 5.77,
      lng: -74.97,
      precio_galon_actual: null,
      precio_acpm: null,
    },
    { id: 'e5', nombre: 'EDS Sin coordenadas', lat: null, lng: null, precio_galon_actual: null },
  ];

  @Component({
    imports: [MapaRecomendacion],
    template: `<app-mapa-recomendacion
      [estaciones]="estaciones()"
      estacionElegidaId="e1"
      [vehiculo]="vehiculo"
      ruta="Bogotá - Medellín"
      [polilinea]="linea"
      [autonomiaKm]="150"
      (distancia)="distancias.push($event)"
      (estacionesCerca)="cercanas.push($event)"
    />`,
  })
  class Host {
    readonly estaciones = signal<EstacionMapa[]>(ESTACIONES);
    readonly vehiculo = VEHICULO;
    readonly linea = LINEA;
    readonly distancias: { km: number; porCarretera: boolean }[] = [];
    readonly cercanas: number[] = [];
  }

  it('reporta la distancia a la EDS y solo cuenta la elegida más las cercanas con ACPM', async () => {
    const fixture = TestBed.createComponent(Host);
    await estabilizar(fixture);
    const host = fixture.componentInstance;

    expect(fixture.nativeElement.querySelector('.leaflet-container')).not.toBeNull();
    // Elegida + cercana (≤ 10 km y con ACPM). La lejana, la sin ACPM y la sin coordenadas no cuentan.
    expect(host.cercanas.at(-1)).toBe(2);
    expect(host.distancias.length).toBeGreaterThan(0);
    const ultima = host.distancias.at(-1)!;
    expect(ultima.km).toBeGreaterThan(0);
    expect(ultima.porCarretera).toBe(false);

    host.estaciones.set([]);
    await estabilizar(fixture);
    expect(host.cercanas.at(-1)).toBe(0); // requiere tanqueo = false => sin estaciones

    fixture.destroy();
  });
});

describe('MapaTablero', () => {
  const EDS: GalonesPorEdsApi[] = [
    {
      id: 'a',
      nombre: 'EDS A',
      lat: 5,
      lng: -74,
      ruta: null,
      proveedor: 'terpel',
      precio: 9000,
      galones: 100,
      tanqueos: 3,
    },
    {
      id: 'b',
      nombre: 'EDS <B>',
      lat: 6,
      lng: -75,
      ruta: null,
      proveedor: null,
      precio: 9500,
      galones: 400,
      tanqueos: 1,
    },
  ];
  const RUTAS: RutaLineaApi[] = [
    {
      ruta: 'bogota-medellin',
      origen_lat: 4.7,
      origen_lng: -74,
      destino_lat: 6.2,
      destino_lng: -75.5,
    },
  ];

  @Component({
    imports: [MapaTablero],
    template: `
      <app-mapa-tablero modo="galones" [estaciones]="estaciones()" />
      <app-mapa-tablero modo="rutas" [rutas]="rutas()" />
    `,
  })
  class Host {
    readonly estaciones = signal<GalonesPorEdsApi[]>(EDS);
    readonly rutas = signal<RutaLineaApi[]>(RUTAS);
  }

  it('muestra ambos mapas con su leyenda y los reemplaza por el aviso cuando no hay datos', async () => {
    const fixture = TestBed.createComponent(Host);
    await estabilizar(fixture);
    const raiz = fixture.nativeElement as HTMLElement;

    expect(raiz.querySelectorAll('.leaflet-container')).toHaveLength(2);
    expect(raiz.textContent).toContain('$9.000/gal');
    expect(raiz.textContent).toContain('$9.500/gal');
    expect(raiz.textContent).toContain('Origen');
    expect(raiz.textContent).toContain('Destino');

    fixture.componentInstance.estaciones.set([]);
    fixture.componentInstance.rutas.set([]);
    await estabilizar(fixture);
    expect(raiz.querySelectorAll('.leaflet-container')).toHaveLength(0);
    expect(raiz.textContent).toContain('Sin tanqueos georreferenciados para el filtro.');
    expect(raiz.textContent).toContain('Sin rutas georreferenciadas para el filtro.');
  });
});

describe('colorCosto', () => {
  it('va de verde (más barato) a rojo (más caro) y es neutro sin datos', () => {
    expect(colorCosto(100, 100, 200)).toBe('hsl(120, 75%, 45%)');
    expect(colorCosto(200, 100, 200)).toBe('hsl(0, 75%, 45%)');
    expect(colorCosto(150, 100, 200)).toBe('hsl(60, 75%, 45%)');
    expect(colorCosto(100, 100, 100)).toBe('hsl(120, 75%, 45%)');
    expect(colorCosto(null, 100, 200)).toBe('#64748B');
    expect(colorCosto(100, null, null)).toBe('#64748B');
  });
});
