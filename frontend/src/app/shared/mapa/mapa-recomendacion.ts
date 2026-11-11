import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  computed,
  effect,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import * as L from 'leaflet';
import { formatFecha } from '../utils/fecha';
import {
  type LatLng,
  distanciaKm,
  indiceMasCercano,
  longitudPolilineaKm,
  posicionSobrePolilineaKm,
  proyectarEnPolilinea,
  rutaPorCarretera,
  subPolilineaPorKm,
} from '../utils/geo';
import {
  COLOR_MAPA,
  crearIconoCamion,
  dibujarFlechasDireccion,
  encuadrar,
  escaparHtml,
  usarMapa,
} from './leaflet-util';
import type { VehiculoMapa } from './mapa-ruta';

export interface EstacionMapa {
  id: string;
  nombre: string;
  lat: number | null;
  lng: number | null;
  precio_galon_actual: number | null;
  precio_acpm?: number | null;
  precio_corriente?: number | null;
  proveedor?: string | null; // 'terpel' | 'distracom'
}

type EstacionConCoords = EstacionMapa & { lat: number; lng: number };

/**
 * Además de la EDS elegida, se muestran las estaciones a <= 10 km (línea recta)
 * de ella, como alternativas cercanas.
 */
export const UMBRAL_CERCANIA_EDS_KM = 10;

/** Nombre legible del proveedor de la EDS. */
const PROVEEDOR_LABEL: Record<string, string> = { terpel: 'Terpel', distracom: 'Distracom' };

/**
 * Mapa de la recomendación: ruta del viaje, tramo de autonomía, EDS elegida y
 * estaciones cercanas, y vehículo sobre la vía. Reporta la distancia a la EDS y
 * cuántas estaciones quedaron visibles.
 */
@Component({
  selector: 'app-mapa-recomendacion',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'block' },
  template: `
    <div class="h-[44rem] w-full overflow-hidden rounded-lg border border-border">
      <div #mapa class="h-full w-full"></div>
    </div>
  `,
})
export class MapaRecomendacion {
  readonly estaciones = input.required<readonly EstacionMapa[]>();
  readonly estacionElegidaId = input.required<string>();
  readonly vehiculo = input<VehiculoMapa | null>(null);
  readonly ruta = input<string | null>(null);
  /** Polilínea real de la ruta del viaje (tabla rutas). Si viene, se dibuja tal cual. */
  readonly polilinea = input<LatLng[] | null>(null);
  /** Autonomía del vehículo (km): alcance del tramo rojo sobre la ruta. */
  readonly autonomiaKm = input<number | null>(null);

  /** Distancia a la EDS (por carretera si OSRM responde; si no, sobre la polilínea). */
  readonly distancia = output<{ km: number; porCarretera: boolean }>();
  /** Cuántas estaciones quedaron dentro del umbral de cercanía a la EDS elegida. */
  readonly estacionesCerca = output<number>();

  private readonly contenedor = viewChild<ElementRef<HTMLElement>>('mapa');
  private readonly mapa = usarMapa(this.contenedor, true);

  private readonly conCoords = computed(() =>
    this.estaciones().filter((e): e is EstacionConCoords => e.lat != null && e.lng != null),
  );

  private readonly elegida = computed(
    () => this.conCoords().find((e) => e.id === this.estacionElegidaId()) ?? null,
  );

  /** Extremos de la ruta (norte y sur) según las estaciones. */
  private readonly extremos = computed<LatLng[]>(() => {
    const estaciones = this.conCoords();
    if (estaciones.length < 2) return estaciones.map((e): LatLng => [e.lat, e.lng]);
    const norte = estaciones.reduce((m, e) => (e.lat > m.lat ? e : m));
    const sur = estaciones.reduce((m, e) => (e.lat < m.lat ? e : m));
    return [
      [norte.lat, norte.lng],
      [sur.lat, sur.lng],
    ];
  });

  /**
   * Waypoints de la ruta: la EDS elegida va como punto de paso
   * (norte → EDS → sur), porque el vehículo debe pasar por ella.
   */
  private readonly waypointsRuta = computed<LatLng[]>(() => {
    const extremos = this.extremos();
    const elegida = this.elegida();
    if (extremos.length < 2 || !elegida) return extremos;
    return [extremos[0], [elegida.lat, elegida.lng], extremos[1]];
  });

  /** Geometría OSRM entre waypoints: SOLO respaldo cuando no hay polilínea real. */
  private readonly geometriaRuta = signal<LatLng[] | null>(null);

  /**
   * Rutas "rectas" (DANE, 2 puntos): se trazan SOBRE carretera con OSRM entre su
   * origen y destino. Las de Valhalla (muchos puntos) ya siguen la vía.
   */
  private readonly polilineaCarretera = signal<LatLng[] | null>(null);

  /**
   * Prioridad: Valhalla (>2 pts) → DANE trazada por carretera (OSRM) → DANE recta
   * → OSRM entre extremos → waypoints rectos.
   */
  private readonly rutaReal = computed<LatLng[] | null>(() => {
    const poli = this.polilinea();
    if (poli && poli.length > 2) return poli;
    return this.polilineaCarretera() ?? (poli && poli.length > 1 ? poli : null);
  });

  private readonly lineaRuta = computed<LatLng[]>(
    () => this.rutaReal() ?? this.geometriaRuta() ?? this.waypointsRuta(),
  );

  /** El vehículo se ubica SOBRE la ruta: se proyecta su última posición al punto más cercano. */
  private readonly vehiculoEnRuta = computed<LatLng | null>(() => {
    const v = this.vehiculo();
    if (!v) return null;
    const linea = this.lineaRuta();
    return linea.length > 1 ? proyectarEnPolilinea([v.lat, v.lng], linea) : [v.lat, v.lng];
  });

  /** Tramo vehículo→EDS sobre la misma polilínea (ambas proyectadas sobre la vía). */
  private readonly lineaVehiculo = computed<LatLng[] | null>(() => {
    const enRuta = this.vehiculoEnRuta();
    const elegida = this.elegida();
    const linea = this.lineaRuta();
    if (!enRuta || !elegida) return null;
    if (linea.length < 2) return [enRuta, [elegida.lat, elegida.lng]];
    const iVeh = indiceMasCercano(linea, enRuta);
    const iEds = indiceMasCercano(linea, [elegida.lat, elegida.lng]);
    return linea.slice(Math.min(iVeh, iEds), Math.max(iVeh, iEds) + 1);
  });

  /**
   * Línea de AUTONOMÍA: alcance real del vehículo sobre la ruta con el tanque actual.
   * Va desde la posición del vehículo hasta min(pos + autonomía, fin de ruta); si la
   * autonomía cubre toda la ruta restante llega hasta el destino (no se detiene en la EDS).
   */
  private readonly lineaAutonomia = computed<LatLng[] | null>(() => {
    const enRuta = this.vehiculoEnRuta();
    const linea = this.lineaRuta();
    const autonomia = this.autonomiaKm();
    if (!enRuta || linea.length < 2 || autonomia == null || autonomia <= 0) {
      return this.lineaVehiculo(); // respaldo: sin ruta/autonomía, tramo vehículo→EDS
    }
    const posVeh = posicionSobrePolilineaKm(enRuta, linea).posKm;
    const total = longitudPolilineaKm(linea);
    const fin = Math.min(posVeh + autonomia, total);
    return subPolilineaPorKm(linea, posVeh, fin) ?? this.lineaVehiculo();
  });

  /**
   * Estaciones a mostrar: la EDS elegida siempre; y además las que estén a
   * <= UMBRAL_CERCANIA_EDS_KM (línea recta) de la elegida y tengan precio de ACPM
   * cargado (la flota es diésel).
   */
  private readonly estacionesVisibles = computed<EstacionConCoords[]>(() => {
    const elegida = this.elegida();
    const id = this.estacionElegidaId();
    const estaciones = this.conCoords();
    if (!elegida) return estaciones.filter((e) => e.id === id);
    return estaciones.filter(
      (e) =>
        e.id === id ||
        (e.precio_acpm != null &&
          distanciaKm(elegida.lat, elegida.lng, e.lat, e.lng) <= UMBRAL_CERCANIA_EDS_KM),
    );
  });

  private readonly puntos = computed<LatLng[]>(() => {
    const p: LatLng[] = this.estacionesVisibles().map((e): LatLng => [e.lat, e.lng]);
    const enRuta = this.vehiculoEnRuta();
    if (enRuta) p.push(enRuta);
    // Extremos de la ruta: aseguran que el encuadre muestre toda la ruta aunque
    // no haya estaciones (p. ej. cuando la autonomía cubre el trayecto).
    const linea = this.lineaRuta();
    if (linea.length > 1) p.push(linea[0], linea[linea.length - 1]);
    return p;
  });

  constructor() {
    effect((alLimpiar) => {
      const poli = this.polilinea();
      this.polilineaCarretera.set(null);
      if (!poli || poli.length !== 2) return;
      const ctrl = new AbortController();
      void rutaPorCarretera([poli[0], poli[1]], ctrl.signal).then((r) => {
        if (r && r.coords.length > 1) this.polilineaCarretera.set(r.coords);
      });
      alLimpiar(() => ctrl.abort());
    });

    // Respaldo OSRM SOLO si no hay polilínea real del viaje.
    effect((alLimpiar) => {
      const rutaReal = this.rutaReal();
      const waypoints = this.waypointsRuta();
      this.geometriaRuta.set(null);
      if (rutaReal || waypoints.length <= 1) return;
      const ctrl = new AbortController();
      void rutaPorCarretera(waypoints, ctrl.signal).then((r) => {
        if (r) this.geometriaRuta.set(r.coords);
      });
      alLimpiar(() => ctrl.abort());
    });

    // Distancia a la EDS = longitud del tramo sobre la carretera.
    effect(() => {
      const tramo = this.lineaVehiculo();
      const porCarretera = this.geometriaRuta() != null;
      if (!tramo || tramo.length < 2) return;
      let km = 0;
      for (let i = 0; i < tramo.length - 1; i += 1) {
        km += distanciaKm(tramo[i][0], tramo[i][1], tramo[i + 1][0], tramo[i + 1][1]);
      }
      this.distancia.emit({ km, porCarretera });
    });

    effect(() => {
      this.estacionesCerca.emit(this.estacionesVisibles().length);
    });

    // Encuadre inicial (una sola vez por mapa); luego respeta el zoom/arrastre.
    let encuadrado: L.Map | null = null;
    effect(() => {
      const mapa = this.mapa();
      const puntos = this.puntos();
      if (!mapa || encuadrado === mapa) return;
      if (encuadrar(mapa, puntos, { zoomUnico: 12, maxZoom: 13 })) encuadrado = mapa;
    });

    effect((alLimpiar) => {
      const mapa = this.mapa();
      if (!mapa) return;
      const linea = this.lineaRuta();
      const autonomia = this.lineaAutonomia();
      const visibles = this.estacionesVisibles();
      const elegidaId = this.estacionElegidaId();
      const vehiculo = this.vehiculo();
      const enRuta = this.vehiculoEnRuta();
      const capa = L.layerGroup().addTo(mapa);
      const limpiezas: (() => void)[] = [];

      if (linea.length > 1) {
        L.polyline(linea, { color: COLOR_MAPA.ruta, weight: 8, opacity: 0.4 })
          .bindTooltip(`Ruta tractomula · ${escaparHtml(this.ruta() ?? '—')}`, { sticky: true })
          .addTo(capa);
        // Flechas de orientación de la ruta (opacidad completa, no la de la línea).
        limpiezas.push(dibujarFlechasDireccion(mapa, linea, COLOR_MAPA.ruta, 18, 8));
      }

      if (autonomia) {
        L.polyline(autonomia, { color: COLOR_MAPA.autonomia, weight: 4, opacity: 0.45 })
          .bindTooltip('Alcance de la autonomía sobre la ruta', { sticky: true })
          .addTo(capa);
        // Autonomía: flechas del tamaño de la línea (opacidad completa).
        limpiezas.push(dibujarFlechasDireccion(mapa, autonomia, COLOR_MAPA.autonomia, 55, 9));
      }

      for (const e of visibles) {
        const esElegida = e.id === elegidaId;
        const proveedor = e.proveedor ? ` · ${PROVEEDOR_LABEL[e.proveedor] ?? e.proveedor}` : '';
        const acpm = e.precio_acpm != null ? `$${e.precio_acpm.toLocaleString('es-CO')}/gal` : '—';
        L.circleMarker([e.lat, e.lng], {
          radius: esElegida ? 11 : 7,
          color: '#fff',
          fillColor: esElegida ? COLOR_MAPA.elegida : COLOR_MAPA.cara,
          fillOpacity: 1,
          weight: esElegida ? 3 : 2,
        })
          // La flota es diésel: solo interesa el ACPM en todas las estaciones.
          .bindTooltip(
            `<span style="font-weight:500">${escaparHtml(e.nombre)}</span>${escaparHtml(proveedor)}${esElegida ? ' · ELEGIDA' : ''}<br>ACPM: ${acpm}`,
          )
          .addTo(capa);
      }

      if (enRuta && vehiculo) {
        L.marker(enRuta, { icon: crearIconoCamion(), zIndexOffset: 1000 })
          .bindTooltip(
            `${escaparHtml(vehiculo.placa)} · sobre la ruta<br>Telemetría: ${formatFecha(vehiculo.ubicacion_at)}`,
          )
          .addTo(capa);
      }

      alLimpiar(() => {
        limpiezas.forEach((f) => f());
        capa.remove();
      });
    });
  }
}
