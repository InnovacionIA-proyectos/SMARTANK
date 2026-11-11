import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  computed,
  effect,
  input,
  signal,
  viewChild,
} from '@angular/core';
import * as L from 'leaflet';
import { formatFecha } from '../utils/fecha';
import {
  type LatLng,
  indiceMasCercano,
  proyectarEnPolilinea,
  rutaPorCarretera,
  tramoAutonomia,
} from '../utils/geo';
import {
  COLOR_MAPA,
  crearIconoCamion,
  dibujarFlechasDireccion,
  encuadrar,
  escaparHtml,
  usarMapa,
} from './leaflet-util';

export interface VehiculoMapa {
  placa: string;
  lat: number;
  lng: number;
  ubicacion_at?: string | null;
}

const km0 = (n: number) => `${n.toLocaleString('es-CO', { maximumFractionDigits: 0 })} km`;

/**
 * Mapa de seguimiento: ruta completa del viaje (con flechas de sentido), tramo de
 * autonomía alcanzable y posición del vehículo "pegada" a la vía.
 */
@Component({
  selector: 'app-mapa-ruta',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'block' },
  template: `
    @if (lineaRuta().length < 2) {
      <div
        class="flex h-[32rem] w-full items-center justify-center rounded-lg border border-border text-caption text-text-muted"
      >
        Este viaje no tiene ruta cargada.
      </div>
    } @else {
      <div class="space-y-2">
        <div class="h-[32rem] w-full overflow-hidden rounded-lg border border-border">
          <div #mapa class="h-full w-full"></div>
        </div>

        <div class="flex flex-wrap items-center gap-4 text-caption text-text-muted">
          <span class="flex items-center gap-1.5">
            <span
              class="inline-block h-1 w-4 rounded-full"
              [style.background]="colores.ruta"
            ></span>
            Ruta{{ kmRuta() != null ? ' · ' + km0(kmRuta()!) : '' }}
          </span>
          <span class="flex items-center gap-1.5">
            <span
              class="inline-block h-1 w-4 rounded-full"
              [style.background]="colores.autonomia"
            ></span>
            Autonomía{{ autonomiaKm() != null ? ' · ' + km0(autonomiaKm()!) : ' · sin dato' }}
          </span>
          @if (!vehiculo()) {
            <span>· sin telemetría de posición del vehículo</span>
          }
        </div>
      </div>
    }
  `,
})
export class MapaRuta {
  /** Polilínea de la ruta [[lat,lng], …] (origen → destino). */
  readonly polilinea = input.required<LatLng[]>();
  /** Última posición del vehículo (telemetría). */
  readonly vehiculo = input<VehiculoMapa | null>(null);
  /** Autonomía del vehículo (km): longitud del tramo alcanzable sobre la ruta. */
  readonly autonomiaKm = input<number | null>(null);
  readonly ruta = input<string | null>(null);
  /** Longitud total de la ruta (km), para la leyenda. */
  readonly kilometros = input<number | null>(null);

  protected readonly colores = COLOR_MAPA;
  protected readonly km0 = km0;

  private readonly contenedor = viewChild<ElementRef<HTMLElement>>('mapa');
  private readonly mapa = usarMapa(this.contenedor, true);

  /**
   * Rutas "rectas" (DANE, 2 puntos): se trazan SOBRE carretera con OSRM. Mientras
   * carga (o si OSRM no responde) se usa la recta original.
   */
  private readonly porCarretera = signal<{ coords: LatLng[]; km: number } | null>(null);

  protected readonly lineaRuta = computed<LatLng[]>(
    () => this.porCarretera()?.coords ?? this.polilinea(),
  );
  protected readonly kmRuta = computed(() => this.porCarretera()?.km ?? this.kilometros());

  /** Vehículo proyectado sobre la ruta (se "pega" a la vía). */
  private readonly vehiculoEnRuta = computed<LatLng | null>(() => {
    const v = this.vehiculo();
    if (!v) return null;
    const linea = this.lineaRuta();
    return linea.length > 1 ? proyectarEnPolilinea([v.lat, v.lng], linea) : [v.lat, v.lng];
  });

  /** Tramo alcanzable con la autonomía actual, sobre la misma ruta. */
  private readonly tramo = computed<LatLng[]>(() => {
    const enRuta = this.vehiculoEnRuta();
    const autonomia = this.autonomiaKm();
    const linea = this.lineaRuta();
    if (!enRuta || autonomia == null || autonomia <= 0 || linea.length < 2) return [];
    return tramoAutonomia(linea, enRuta, indiceMasCercano(linea, enRuta), autonomia);
  });

  /** ¿La autonomía cubre toda la ruta restante? (el tramo llegó al último vértice) */
  private readonly alcanzaDestino = computed(() => {
    const tramo = this.tramo();
    const linea = this.lineaRuta();
    if (tramo.length < 2 || linea.length === 0) return false;
    const fin = tramo[tramo.length - 1];
    const ultimo = linea[linea.length - 1];
    return fin[0] === ultimo[0] && fin[1] === ultimo[1];
  });

  constructor() {
    effect((alLimpiar) => {
      const polilinea = this.polilinea();
      this.porCarretera.set(null);
      if (polilinea.length !== 2) return; // ya es una polilínea de carretera (Valhalla)
      const ctrl = new AbortController();
      void rutaPorCarretera([polilinea[0], polilinea[1]], ctrl.signal).then((r) => {
        if (r && r.coords.length > 1)
          this.porCarretera.set({ coords: r.coords, km: r.distanciaKm });
      });
      alLimpiar(() => ctrl.abort());
    });

    // Encuadre inicial (una sola vez por mapa).
    let encuadrado: L.Map | null = null;
    effect(() => {
      const mapa = this.mapa();
      const linea = this.lineaRuta();
      if (!mapa || encuadrado === mapa) return;
      if (encuadrar(mapa, linea, { zoomUnico: 12, maxZoom: 13 })) encuadrado = mapa;
    });

    effect((alLimpiar) => {
      const mapa = this.mapa();
      if (!mapa) return;
      const linea = this.lineaRuta();
      const tramo = this.tramo();
      const vehiculo = this.vehiculo();
      const enRuta = this.vehiculoEnRuta();
      const autonomia = this.autonomiaKm();
      const capa = L.layerGroup().addTo(mapa);
      const limpiezas: (() => void)[] = [];

      // Ruta completa (línea tenue + flechas de sentido)
      L.polyline(linea, { color: COLOR_MAPA.ruta, weight: 8, opacity: 0.4 })
        .bindTooltip(`Ruta · ${escaparHtml(this.ruta() ?? '—')}`, { sticky: true })
        .addTo(capa);
      limpiezas.push(dibujarFlechasDireccion(mapa, linea, COLOR_MAPA.ruta, 18, 8));

      // Tramo de autonomía, ENCIMA de la ruta
      if (tramo.length > 1) {
        L.polyline(tramo, { color: COLOR_MAPA.autonomia, weight: 4, opacity: 0.45 })
          .bindTooltip(
            `Autonomía · ${autonomia != null ? km0(autonomia) : '—'}${this.alcanzaDestino() ? ' (cubre toda la ruta)' : ''}`,
            { sticky: true },
          )
          .addTo(capa);
        limpiezas.push(dibujarFlechasDireccion(mapa, tramo, COLOR_MAPA.autonomia, 55, 9));
      }

      // Vehículo (camión sobre la ruta)
      if (enRuta && vehiculo) {
        L.marker(enRuta, { icon: crearIconoCamion(), zIndexOffset: 1000 })
          .bindTooltip(
            `${escaparHtml(vehiculo.placa)} · posición actual<br>Telemetría: ${formatFecha(vehiculo.ubicacion_at)}`,
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
