import { ElementRef, Signal, effect, signal, untracked } from '@angular/core';
import * as L from 'leaflet';
import type { LatLng } from '../utils/geo';

/** Escapa texto para insertarlo en el HTML de un tooltip de Leaflet. */
export function escaparHtml(texto: string): string {
  return texto
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#39;');
}

/** Colores de los trazos de mapa (valores del design system, fijos en ambos temas). */
export const COLOR_MAPA = {
  barata: '#069E49',
  cara: '#B45309',
  elegida: '#0154BA',
  vehiculo: '#0F1B2D',
  ruta: '#069E49', // verde de marca: ruta completa
  autonomia: '#C62828', // rojo: tramo alcanzable con la autonomía actual
  neutro: '#64748B',
  origen: '#069E49',
  destino: '#C62828',
  linea: '#0154BA',
} as const;

const TILES_URL = 'https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png';
const TILES_ATRIBUCION = '&copy; OpenStreetMap contributors';

/** Crea el mapa base (vista de carretera de OpenStreetMap, sin API key). */
export function crearMapaBase(contenedor: HTMLElement, preferCanvas: boolean): L.Map {
  const mapa = L.map(contenedor, { scrollWheelZoom: true, preferCanvas });
  L.tileLayer(TILES_URL, { attribution: TILES_ATRIBUCION, maxZoom: 19 }).addTo(mapa);
  return mapa;
}

/**
 * Ciclo de vida de un mapa Leaflet ligado a un elemento del template: crea el mapa
 * cuando el elemento existe y lo destruye cuando desaparece o el componente se
 * destruye. Devuelve una señal con el mapa (o null mientras no hay contenedor).
 * Debe llamarse en contexto de inyección.
 */
export function usarMapa(
  contenedor: Signal<ElementRef<HTMLElement> | undefined>,
  preferCanvas: boolean | (() => boolean),
): Signal<L.Map | null> {
  const mapa = signal<L.Map | null>(null);
  effect((alLimpiar) => {
    const el = contenedor()?.nativeElement;
    if (!el) return;
    const instancia = untracked(() =>
      crearMapaBase(el, typeof preferCanvas === 'function' ? preferCanvas() : preferCanvas),
    );
    mapa.set(instancia);
    alLimpiar(() => {
      instancia.remove();
      mapa.set(null);
    });
  });
  return mapa;
}

/**
 * Encuadra el mapa una sola vez (al tener puntos); luego respeta el zoom/arrastre
 * del usuario. Devuelve `true` si encuadró en esta llamada.
 */
export function encuadrar(
  mapa: L.Map,
  puntos: LatLng[],
  opciones: { zoomUnico: number; maxZoom: number },
): boolean {
  if (puntos.length === 0) return false;
  if (puntos.length === 1) mapa.setView(puntos[0], opciones.zoomUnico);
  else mapa.fitBounds(puntos, { padding: [32, 32], maxZoom: opciones.maxZoom });
  return true;
}

const TAM_CAMION = 40;

/** Marcador del vehículo (camión en insignia circular). Se dibuja SOBRE líneas y estaciones. */
export function crearIconoCamion(): L.DivIcon {
  return L.divIcon({
    className: '',
    html: `<div style="width:${TAM_CAMION}px;height:${TAM_CAMION}px;border-radius:50%;background:#fff;border:2px solid ${COLOR_MAPA.vehiculo};display:flex;align-items:center;justify-content:center;box-shadow:0 1px 4px rgba(0,0,0,.45)">
    <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="${COLOR_MAPA.vehiculo}" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
      <path d="M14 18V6a1 1 0 0 0-1-1H3a1 1 0 0 0-1 1v10a1 1 0 0 0 1 1h1"/>
      <path d="M14 9h4l4 4v3a1 1 0 0 1-1 1h-1"/>
      <circle cx="7" cy="18" r="2"/>
      <circle cx="17" cy="18" r="2"/>
    </svg>
  </div>`,
    iconSize: [TAM_CAMION, TAM_CAMION],
    iconAnchor: [TAM_CAMION / 2, TAM_CAMION / 2],
  });
}

/**
 * Dibuja flechas a lo largo de una polilínea para indicar el sentido de avance.
 * Se colocan cada `espaciado` px (en pantalla), orientadas según el segmento; se
 * recalculan al terminar el zoom para mantener el ángulo correcto. Devuelve la
 * función que las retira del mapa.
 */
export function dibujarFlechasDireccion(
  mapa: L.Map,
  positions: LatLng[],
  color: string,
  espaciado = 90,
  tam = 16,
): () => void {
  if (positions.length < 2) return () => undefined;
  const capa = L.layerGroup().addTo(mapa);
  // Cache de iconos por ángulo (redondeado) para no recrear el mismo SVG.
  const iconos = new Map<number, L.DivIcon>();
  const iconoPara = (angulo: number) => {
    const key = Math.round(angulo / 5) * 5; // agrupa ángulos similares
    let icon = iconos.get(key);
    if (!icon) {
      icon = L.divIcon({
        className: '',
        html: `<svg width="${tam}" height="${tam}" viewBox="0 0 24 24" style="transform:rotate(${key}deg);display:block"><path d="M7 4 L17 12 L7 20" fill="none" stroke="${color}" stroke-width="4" stroke-linecap="round" stroke-linejoin="round"/></svg>`,
        iconSize: [tam, tam],
        iconAnchor: [tam / 2, tam / 2],
      });
      iconos.set(key, icon);
    }
    return icon;
  };

  const dibujar = () => {
    capa.clearLayers();
    const pts = positions.map((p) => mapa.latLngToLayerPoint(L.latLng(p[0], p[1])));
    let total = 0;
    for (let i = 1; i < pts.length; i += 1) total += pts[i - 1].distanceTo(pts[i]);
    if (total === 0) return;
    const MAX_FLECHAS = 500;
    const paso = Math.max(espaciado, total / MAX_FLECHAS);
    let acumulado = 0;
    let proximo = paso / 2; // primera flecha a mitad del primer intervalo
    for (let i = 1; i < pts.length; i += 1) {
      const a = pts[i - 1];
      const b = pts[i];
      const largo = a.distanceTo(b);
      if (largo === 0) continue;
      const angulo = (Math.atan2(b.y - a.y, b.x - a.x) * 180) / Math.PI; // grados (y hacia abajo)
      while (acumulado + largo >= proximo) {
        const t = (proximo - acumulado) / largo;
        const latlng = mapa.layerPointToLatLng(
          L.point(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t),
        );
        L.marker(latlng, { icon: iconoPara(angulo), interactive: false, keyboard: false }).addTo(
          capa,
        );
        proximo += paso;
      }
      acumulado += largo;
    }
  };

  dibujar();
  // Solo al terminar el zoom: al arrastrar, Leaflet reubica los marcadores por
  // su latlng sin que cambien los ángulos, así que no hay que recalcular.
  mapa.on('zoomend', dibujar);
  return () => {
    mapa.off('zoomend', dibujar);
    capa.remove();
  };
}
