import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  computed,
  effect,
  input,
  viewChild,
} from '@angular/core';
import * as L from 'leaflet';
import type { GalonesPorEdsApi, RutaLineaApi } from '../../core/api/api-types';
import type { LatLng } from '../utils/geo';
import { COLOR_MAPA, encuadrar, escaparHtml, usarMapa } from './leaflet-util';

const galon = (n: number) => `${n.toLocaleString('es-CO')} gal`;
const cop = (n: number) => `$${n.toLocaleString('es-CO')}`;

/** Escala de costo verde (más económico) → rojo (más costoso) por precio/galón. */
export function colorCosto(precio: number | null, min: number | null, max: number | null): string {
  if (precio == null || min == null || max == null) return COLOR_MAPA.neutro;
  const t = max === min ? 0 : (precio - min) / (max - min); // 0 = barato, 1 = caro
  const hue = (1 - t) * 120; // 120° verde → 0° rojo
  return `hsl(${Math.round(hue)}, 75%, 45%)`;
}

/**
 * Mapas del tablero:
 *  · `galones`: burbujas por consumo de cada EDS, coloreadas por costo.
 *  · `rutas`: origen y destino de cada ruta unidos por una línea recta.
 */
@Component({
  selector: 'app-mapa-tablero',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'block' },
  template: `
    @if (vacio()) {
      <div
        class="flex h-[28rem] w-full items-center justify-center rounded-lg border border-border text-caption text-text-muted"
      >
        {{
          modo() === 'rutas'
            ? 'Sin rutas georreferenciadas para el filtro.'
            : 'Sin tanqueos georreferenciados para el filtro.'
        }}
      </div>
    } @else {
      <div class="space-y-2">
        <div class="h-[28rem] w-full overflow-hidden rounded-lg border border-border">
          <div #mapa class="h-full w-full"></div>
        </div>

        @if (modo() === 'rutas') {
          <div class="flex flex-wrap items-center gap-4 text-caption text-text-muted">
            <span class="flex items-center gap-1.5">
              <span
                class="inline-block h-3 w-3 rounded-full"
                [style.background]="colores.origen"
              ></span>
              Origen
            </span>
            <span class="flex items-center gap-1.5">
              <span
                class="inline-block h-3 w-3 rounded-full"
                [style.background]="colores.destino"
              ></span>
              Destino
            </span>
          </div>
        } @else if (rangoPrecios(); as rango) {
          <div class="flex items-center gap-2 text-caption text-text-muted">
            <span>{{ cop(rango.min) }}/gal</span>
            <span
              class="h-2 flex-1 rounded-full"
              style="background: linear-gradient(to right, hsl(120,75%,45%), hsl(60,75%,45%), hsl(0,75%,45%))"
            ></span>
            <span>{{ cop(rango.max) }}/gal</span>
          </div>
        }
      </div>
    }
  `,
})
export class MapaTablero {
  /** 'galones': burbujas por consumo (EDS). 'rutas': origen→destino con línea recta. */
  readonly modo = input.required<'galones' | 'rutas'>();
  readonly estaciones = input<readonly GalonesPorEdsApi[]>([]);
  readonly rutas = input<readonly RutaLineaApi[]>([]);

  protected readonly colores = COLOR_MAPA;
  protected readonly cop = cop;

  private readonly contenedor = viewChild<ElementRef<HTMLElement>>('mapa');
  // Las rutas se dibujan en canvas; el mapa de galones usa SVG (como el original).
  private readonly mapa = usarMapa(this.contenedor, () => this.modo() === 'rutas');

  protected readonly vacio = computed(() =>
    this.modo() === 'rutas' ? this.rutas().length === 0 : this.estaciones().length === 0,
  );

  protected readonly rangoPrecios = computed(() => {
    const precios = this.estaciones()
      .map((e) => e.precio)
      .filter((p): p is number => p != null);
    return precios.length ? { min: Math.min(...precios), max: Math.max(...precios) } : null;
  });

  private readonly puntos = computed<LatLng[]>(() =>
    this.modo() === 'rutas'
      ? this.rutas().flatMap((r): LatLng[] => [
          [r.origen_lat, r.origen_lng],
          [r.destino_lat, r.destino_lng],
        ])
      : this.estaciones().map((e): LatLng => [e.lat, e.lng]),
  );

  constructor() {
    // Reencuadra cuando cambian los datos (los filtros del tablero los reemplazan).
    effect(() => {
      const mapa = this.mapa();
      const puntos = this.puntos();
      if (!mapa) return;
      encuadrar(mapa, puntos, { zoomUnico: 9, maxZoom: 11 });
    });

    effect((alLimpiar) => {
      const mapa = this.mapa();
      if (!mapa) return;
      const capa = L.layerGroup().addTo(mapa);

      if (this.modo() === 'rutas') {
        const rutas = this.rutas();
        // Línea recta origen→destino por ruta
        for (const r of rutas) {
          L.polyline(
            [
              [r.origen_lat, r.origen_lng],
              [r.destino_lat, r.destino_lng],
            ],
            { color: COLOR_MAPA.linea, weight: 2, opacity: 0.6 },
          )
            .bindTooltip(`Ruta · ${escaparHtml(r.ruta ?? '—')}`, { sticky: true })
            .addTo(capa);
        }
        // Origen (verde) y destino (rojo) de cada ruta
        for (const r of rutas) {
          L.circleMarker([r.origen_lat, r.origen_lng], {
            radius: 5,
            color: '#fff',
            fillColor: COLOR_MAPA.origen,
            fillOpacity: 1,
            weight: 1.5,
          })
            .bindTooltip(`Origen · ${escaparHtml(r.ruta ?? '—')}`)
            .addTo(capa);
        }
        for (const r of rutas) {
          L.circleMarker([r.destino_lat, r.destino_lng], {
            radius: 5,
            color: '#fff',
            fillColor: COLOR_MAPA.destino,
            fillOpacity: 1,
            weight: 1.5,
          })
            .bindTooltip(`Destino · ${escaparHtml(r.ruta ?? '—')}`)
            .addTo(capa);
        }
      } else {
        // Galones por EDS (burbujas por consumo, color por costo).
        const estaciones = this.estaciones();
        const maxGalones = estaciones.reduce((m, e) => Math.max(m, e.galones), 0) || 1;
        const rango = this.rangoPrecios();
        for (const e of estaciones) {
          const fill = colorCosto(e.precio, rango?.min ?? null, rango?.max ?? null);
          const radius = 6 + Math.round((e.galones / maxGalones) * 22);
          const proveedor = e.proveedor ? ` · ${e.proveedor}` : '';
          const precio = e.precio != null ? `${cop(e.precio)}/gal · ` : '';
          L.circleMarker([e.lat, e.lng], {
            radius,
            color: '#fff',
            fillColor: fill,
            fillOpacity: 0.85,
            weight: 2,
          })
            .bindTooltip(
              `<span style="font-weight:500">${escaparHtml(e.nombre)}</span>${escaparHtml(proveedor)}<br>${precio}${galon(e.galones)} · ${e.tanqueos} tanqueo${e.tanqueos === 1 ? '' : 's'}`,
            )
            .addTo(capa);
        }
      }

      alLimpiar(() => capa.remove());
    });
  }
}
