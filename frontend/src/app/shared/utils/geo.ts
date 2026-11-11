export type LatLng = [number, number];

/** Radio terrestre medio (km). */
const R_KM = 6371;

const aRad = (grados: number) => (grados * Math.PI) / 180;

/**
 * Ruta por carretera entre puntos usando OSRM (router.project-osrm.org, público,
 * sin API key). Devuelve la geometría sobre vías y la distancia en km, o null si
 * falla la red (el llamador hace fallback a línea recta). Se usa `fetch` y no el
 * `HttpClient` de la app para no enviar credenciales ni pasar por el interceptor.
 */
export async function rutaPorCarretera(
  puntos: LatLng[],
  signal?: AbortSignal,
): Promise<{ coords: LatLng[]; distanciaKm: number } | null> {
  if (puntos.length < 2) return null;
  const coords = puntos.map(([lat, lng]) => `${lng},${lat}`).join(';');
  const url = `https://router.project-osrm.org/route/v1/driving/${coords}?overview=full&geometries=geojson`;
  try {
    const res = await fetch(url, { signal, credentials: 'omit' });
    if (!res.ok) return null;
    const data = await res.json();
    const route = data?.routes?.[0];
    if (!route) return null;
    return {
      coords: (route.geometry.coordinates as [number, number][]).map(([lng, lat]) => [lat, lng]),
      distanciaKm: route.distance / 1000,
    };
  } catch {
    return null; // red no disponible / abortado
  }
}

/** Distancia en kilómetros entre dos puntos (fórmula de haversine). */
export function distanciaKm(aLat: number, aLng: number, bLat: number, bLng: number): number {
  const dLat = aRad(bLat - aLat);
  const dLng = aRad(bLng - aLng);
  const h =
    Math.sin(dLat / 2) ** 2 + Math.cos(aRad(aLat)) * Math.cos(aRad(bLat)) * Math.sin(dLng / 2) ** 2;
  return 2 * R_KM * Math.asin(Math.sqrt(h));
}

/**
 * Proyección equirectangular local (km) alrededor de una latitud de referencia.
 * Suficientemente exacta para tramos y umbrales de pocos kilómetros.
 */
function aPlanoKm(latRef: number): (p: LatLng) => [number, number] {
  const cosRef = Math.cos(latRef);
  return ([lat, lng]) => [aRad(lng) * cosRef * R_KM, aRad(lat) * R_KM];
}

interface ProyeccionSegmento {
  /** Punto proyectado (coordenadas planas km). */
  cx: number;
  cy: number;
  /** Parámetro t en [0,1] a lo largo del segmento. */
  t: number;
  /** Distancia perpendicular (km) del punto al segmento. */
  dist: number;
  /** Longitud del segmento (km). */
  largo: number;
  latRef: number;
}

/** Proyecta `p` sobre el segmento a→b (acotado a los extremos). */
function proyectarEnSegmento(p: LatLng, a: LatLng, b: LatLng): ProyeccionSegmento {
  const latRef = aRad((a[0] + b[0]) / 2);
  const xy = aPlanoKm(latRef);
  const [px, py] = xy(p);
  const [ax, ay] = xy(a);
  const [bx, by] = xy(b);
  const dx = bx - ax;
  const dy = by - ay;
  const len2 = dx * dx + dy * dy;
  let t = len2 === 0 ? 0 : ((px - ax) * dx + (py - ay) * dy) / len2;
  t = Math.max(0, Math.min(1, t)); // proyección acotada al segmento
  const cx = ax + t * dx;
  const cy = ay + t * dy;
  return { cx, cy, t, dist: Math.hypot(px - cx, py - cy), largo: Math.sqrt(len2), latRef };
}

/**
 * Punto MÁS CERCANO sobre una polilínea a un punto dado (proyección acotada a
 * cada segmento, quedándose con el de menor distancia). Sirve para "pegar" un
 * marcador a la ruta: se ubica el vehículo sobre la línea estimada del ruta.
 */
export function proyectarEnPolilinea(punto: LatLng, polilinea: LatLng[]): LatLng {
  if (polilinea.length === 0) return punto;
  if (polilinea.length === 1) return polilinea[0];
  let mejor: LatLng = polilinea[0];
  let mejorDist = Infinity;
  for (let i = 0; i < polilinea.length - 1; i += 1) {
    const s = proyectarEnSegmento(punto, polilinea[i], polilinea[i + 1]);
    if (s.dist < mejorDist) {
      mejorDist = s.dist;
      mejor = [
        (s.cy / R_KM) * (180 / Math.PI), // lat
        (s.cx / (R_KM * Math.cos(s.latRef))) * (180 / Math.PI), // lng
      ];
    }
  }
  return mejor;
}

/**
 * Distancia mínima (km) de un punto a una polilínea (el mínimo sobre todos sus
 * segmentos). Se usa para quedarse solo con las estaciones que están muy cerca
 * de la ruta calculada.
 */
export function distanciaAPolilineaKm(punto: LatLng, polilinea: LatLng[]): number {
  if (polilinea.length === 0) return Infinity;
  if (polilinea.length === 1) {
    return distanciaKm(punto[0], punto[1], polilinea[0][0], polilinea[0][1]);
  }
  let min = Infinity;
  for (let i = 0; i < polilinea.length - 1; i += 1) {
    const d = proyectarEnSegmento(punto, polilinea[i], polilinea[i + 1]).dist;
    if (d < min) min = d;
  }
  return min;
}

/**
 * Ubica un punto SOBRE una polilínea: devuelve la distancia recorrida a lo largo
 * de la ruta (km) hasta la proyección del punto, y la distancia perpendicular
 * (km) del punto a la ruta. Proyecta sobre segmentos (no sobre vértices), así que
 * es exacta incluso en polilíneas de pocos puntos (p. ej. rutas DANE de 2 puntos).
 */
export function posicionSobrePolilineaKm(
  punto: LatLng,
  polilinea: LatLng[],
): { posKm: number; distKm: number } {
  if (polilinea.length === 0) return { posKm: 0, distKm: Infinity };
  if (polilinea.length === 1) {
    return { posKm: 0, distKm: distanciaKm(punto[0], punto[1], polilinea[0][0], polilinea[0][1]) };
  }
  let acumulado = 0; // longitud recorrida hasta el inicio del segmento actual
  let mejorPos = 0;
  let mejorDist = Infinity;
  for (let i = 0; i < polilinea.length - 1; i += 1) {
    const s = proyectarEnSegmento(punto, polilinea[i], polilinea[i + 1]);
    if (s.dist < mejorDist) {
      mejorDist = s.dist;
      mejorPos = acumulado + s.t * s.largo;
    }
    acumulado += s.largo;
  }
  return { posKm: mejorPos, distKm: mejorDist };
}

/**
 * Fracción [0,1] de avance de un punto sobre la recta origen→destino (proyección
 * en plano equirectangular local). Aproxima el "% recorrido" del vehículo sobre
 * la ruta sin necesitar la polilínea completa. 0 = en el origen, 1 = en destino.
 */
export function progresoRuta(origen: LatLng, destino: LatLng, punto: LatLng): number {
  const xy = aPlanoKm(aRad((origen[0] + destino[0]) / 2));
  const [ox, oy] = xy(origen);
  const [dxp, dyp] = xy(destino);
  const [px, py] = xy(punto);
  const dx = dxp - ox;
  const dy = dyp - oy;
  const len2 = dx * dx + dy * dy;
  if (len2 === 0) return 0;
  const t = ((px - ox) * dx + (py - oy) * dy) / len2;
  return Math.max(0, Math.min(1, t));
}

/** Longitud total (km) de una polilínea. */
export function longitudPolilineaKm(polilinea: LatLng[]): number {
  let total = 0;
  for (let i = 1; i < polilinea.length; i += 1) {
    total += distanciaKm(
      polilinea[i - 1][0],
      polilinea[i - 1][1],
      polilinea[i][0],
      polilinea[i][1],
    );
  }
  return total;
}

/** Índice del vértice de la polilínea más cercano a un punto (distancia haversine). */
export function indiceMasCercano(polilinea: LatLng[], punto: LatLng): number {
  let mejorI = 0;
  let mejorD = Infinity;
  for (let i = 0; i < polilinea.length; i += 1) {
    const d = distanciaKm(punto[0], punto[1], polilinea[i][0], polilinea[i][1]);
    if (d < mejorD) {
      mejorD = d;
      mejorI = i;
    }
  }
  return mejorI;
}

/**
 * Sub-polilínea entre dos distancias acumuladas (km) sobre `polilinea`, interpolando
 * los puntos exactos de inicio y fin. Devuelve null si el tramo es inválido.
 */
export function subPolilineaPorKm(
  polilinea: LatLng[],
  kmIni: number,
  kmFin: number,
): LatLng[] | null {
  if (polilinea.length < 2 || kmFin <= kmIni) return null;
  const out: LatLng[] = [];
  let acc = 0;
  for (let i = 1; i < polilinea.length; i += 1) {
    const a = polilinea[i - 1];
    const b = polilinea[i];
    const seg = distanciaKm(a[0], a[1], b[0], b[1]);
    if (seg <= 0) continue;
    const ini = acc;
    const fin = acc + seg;
    if (fin >= kmIni && ini <= kmFin) {
      if (out.length === 0) {
        const t = Math.min(1, Math.max(0, (kmIni - ini) / seg));
        out.push([a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t]);
      }
      if (fin >= kmFin) {
        const t = Math.min(1, Math.max(0, (kmFin - ini) / seg));
        out.push([a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t]);
        break;
      }
      out.push(b);
    }
    acc = fin;
  }
  return out.length >= 2 ? out : null;
}

/**
 * Tramo de la ruta alcanzable con `maxKm` km, empezando en `inicio` (posición del
 * vehículo proyectada sobre la vía) y avanzando hacia el destino (fin de la
 * polilínea). Interpola el punto final exacto donde se agota la autonomía.
 */
export function tramoAutonomia(
  polilinea: LatLng[],
  inicio: LatLng,
  indiceInicio: number,
  maxKm: number,
): LatLng[] {
  if (maxKm <= 0 || polilinea.length < 2) return [];
  const out: LatLng[] = [inicio];
  let acc = 0;
  let prev = inicio;
  for (let i = indiceInicio + 1; i < polilinea.length; i += 1) {
    const nxt = polilinea[i];
    const d = distanciaKm(prev[0], prev[1], nxt[0], nxt[1]);
    if (acc + d >= maxKm) {
      const t = d === 0 ? 0 : (maxKm - acc) / d;
      out.push([prev[0] + (nxt[0] - prev[0]) * t, prev[1] + (nxt[1] - prev[1]) * t]);
      return out;
    }
    out.push(nxt);
    acc += d;
    prev = nxt;
  }
  return out; // la autonomía alcanza (o supera) el final de la ruta
}
