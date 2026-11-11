import {
  type LatLng,
  distanciaAPolilineaKm,
  distanciaKm,
  indiceMasCercano,
  longitudPolilineaKm,
  posicionSobrePolilineaKm,
  progresoRuta,
  proyectarEnPolilinea,
  rutaPorCarretera,
  subPolilineaPorKm,
  tramoAutonomia,
} from './geo';

/** Longitud de un grado de longitud sobre el ecuador con R = 6371 km. */
const KM_POR_GRADO = (2 * Math.PI * 6371) / 360;

describe('distanciaKm', () => {
  it('es cero entre el mismo punto', () => {
    expect(distanciaKm(4.711, -74.0721, 4.711, -74.0721)).toBe(0);
  });

  it('un grado de longitud en el ecuador mide ~111,19 km', () => {
    expect(distanciaKm(0, 0, 0, 1)).toBeCloseTo(KM_POR_GRADO, 6);
  });

  it('Bogotá–Medellín está a ~240 km en línea recta', () => {
    const d = distanciaKm(4.711, -74.0721, 6.2442, -75.5812);
    expect(d).toBeGreaterThan(235);
    expect(d).toBeLessThan(245);
  });
});

describe('progresoRuta', () => {
  const origen: LatLng = [0, 0];
  const destino: LatLng = [0, 2];

  it('devuelve 0.5 en el punto medio', () => {
    expect(progresoRuta(origen, destino, [0, 1])).toBeCloseTo(0.5, 9);
  });

  it('acota a [0,1] fuera del tramo', () => {
    expect(progresoRuta(origen, destino, [0, -1])).toBe(0);
    expect(progresoRuta(origen, destino, [0, 5])).toBe(1);
  });

  it('proyecta puntos fuera del eje sobre la recta', () => {
    expect(progresoRuta(origen, destino, [1, 0.5])).toBeCloseTo(0.25, 9);
  });

  it('devuelve 0 si origen y destino coinciden', () => {
    expect(progresoRuta([3, 3], [3, 3], [4, 4])).toBe(0);
  });
});

describe('proyectarEnPolilinea', () => {
  const linea: LatLng[] = [
    [0, 0],
    [0, 2],
  ];

  it('pega un punto al segmento más cercano', () => {
    const p = proyectarEnPolilinea([1, 1], linea);
    expect(p[0]).toBeCloseTo(0, 9);
    expect(p[1]).toBeCloseTo(1, 9);
  });

  it('acota a los extremos del segmento', () => {
    const p = proyectarEnPolilinea([0, 5], linea);
    expect(p[0]).toBeCloseTo(0, 9);
    expect(p[1]).toBeCloseTo(2, 9);
  });

  it('con polilíneas degeneradas devuelve el punto o el único vértice', () => {
    expect(proyectarEnPolilinea([1, 2], [])).toEqual([1, 2]);
    expect(proyectarEnPolilinea([1, 2], [[5, 6]])).toEqual([5, 6]);
  });

  it('elige el segmento más cercano en una polilínea con varios tramos', () => {
    const l: LatLng[] = [
      [0, 0],
      [0, 1],
      [1, 1],
    ];
    const p = proyectarEnPolilinea([0.5, 1.2], l);
    expect(p[0]).toBeCloseTo(0.5, 6);
    expect(p[1]).toBeCloseTo(1, 6);
  });
});

describe('distanciaAPolilineaKm', () => {
  const linea: LatLng[] = [
    [0, 0],
    [0, 2],
  ];

  it('es ~0 para un punto sobre la línea', () => {
    expect(distanciaAPolilineaKm([0, 1], linea)).toBeCloseTo(0, 6);
  });

  it('mide la distancia perpendicular al segmento', () => {
    expect(distanciaAPolilineaKm([0.1, 1], linea)).toBeCloseTo(KM_POR_GRADO * 0.1, 3);
  });

  it('usa haversine con un solo punto y Infinity sin puntos', () => {
    expect(distanciaAPolilineaKm([0, 1], [[0, 0]])).toBeCloseTo(KM_POR_GRADO, 6);
    expect(distanciaAPolilineaKm([0, 1], [])).toBe(Infinity);
  });
});

describe('posicionSobrePolilineaKm', () => {
  it('devuelve la distancia recorrida hasta la proyección', () => {
    const linea: LatLng[] = [
      [0, 0],
      [0, 2],
    ];
    const r = posicionSobrePolilineaKm([0, 0.5], linea);
    expect(r.posKm).toBeCloseTo(KM_POR_GRADO * 0.5, 4);
    expect(r.distKm).toBeCloseTo(0, 6);
  });

  it('acumula la longitud de los segmentos anteriores', () => {
    const linea: LatLng[] = [
      [0, 0],
      [0, 1],
      [0, 2],
    ];
    const r = posicionSobrePolilineaKm([0, 1.5], linea);
    expect(r.posKm).toBeCloseTo(KM_POR_GRADO * 1.5, 4);
  });

  it('casos degenerados', () => {
    expect(posicionSobrePolilineaKm([0, 0], [])).toEqual({ posKm: 0, distKm: Infinity });
    const uno = posicionSobrePolilineaKm([0, 1], [[0, 0]]);
    expect(uno.posKm).toBe(0);
    expect(uno.distKm).toBeCloseTo(KM_POR_GRADO, 6);
  });
});

describe('longitudPolilineaKm', () => {
  it('suma los tramos', () => {
    const l: LatLng[] = [
      [0, 0],
      [0, 1],
      [0, 3],
    ];
    expect(longitudPolilineaKm(l)).toBeCloseTo(KM_POR_GRADO * 3, 6);
  });

  it('es 0 con menos de dos puntos', () => {
    expect(longitudPolilineaKm([])).toBe(0);
    expect(longitudPolilineaKm([[1, 1]])).toBe(0);
  });
});

describe('indiceMasCercano', () => {
  it('devuelve el vértice con menor distancia', () => {
    const l: LatLng[] = [
      [0, 0],
      [0, 1],
      [0, 2],
    ];
    expect(indiceMasCercano(l, [0, 1.8])).toBe(2);
    expect(indiceMasCercano(l, [0, 0.2])).toBe(0);
  });
});

describe('subPolilineaPorKm', () => {
  const linea: LatLng[] = [
    [0, 0],
    [0, 1],
    [0, 2],
  ];

  it('recorta e interpola el tramo entre dos distancias', () => {
    const t = subPolilineaPorKm(linea, KM_POR_GRADO * 0.5, KM_POR_GRADO * 1.5);
    expect(t).not.toBeNull();
    expect(t![0][1]).toBeCloseTo(0.5, 6);
    expect(t![t!.length - 1][1]).toBeCloseTo(1.5, 6);
    expect(t).toHaveLength(3); // inicio interpolado, vértice intermedio, fin interpolado
  });

  it('devuelve null para tramos inválidos', () => {
    expect(subPolilineaPorKm(linea, 10, 10)).toBeNull();
    expect(subPolilineaPorKm(linea, 20, 10)).toBeNull();
    expect(subPolilineaPorKm([[0, 0]], 0, 10)).toBeNull();
  });
});

describe('tramoAutonomia', () => {
  const linea: LatLng[] = [
    [0, 0],
    [0, 1],
    [0, 2],
  ];

  it('corta el tramo exactamente donde se agota la autonomía', () => {
    const t = tramoAutonomia(linea, [0, 0], 0, KM_POR_GRADO * 1.5);
    expect(t[0]).toEqual([0, 0]);
    expect(t[t.length - 1][1]).toBeCloseTo(1.5, 6);
  });

  it('llega al final si la autonomía supera la ruta restante', () => {
    const t = tramoAutonomia(linea, [0, 0], 0, 10_000);
    expect(t[t.length - 1]).toEqual([0, 2]);
  });

  it('sin autonomía o sin ruta no hay tramo', () => {
    expect(tramoAutonomia(linea, [0, 0], 0, 0)).toEqual([]);
    expect(tramoAutonomia([[0, 0]], [0, 0], 0, 50)).toEqual([]);
  });
});

describe('rutaPorCarretera', () => {
  afterEach(() => vi.unstubAllGlobals());

  it('devuelve null con menos de dos puntos sin llamar a la red', async () => {
    const fetchMock = vi.fn();
    vi.stubGlobal('fetch', fetchMock);
    expect(await rutaPorCarretera([[0, 0]])).toBeNull();
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it('convierte la geometría GeoJSON [lng,lat] a [lat,lng] y la distancia a km', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue({
        ok: true,
        json: async () => ({
          routes: [
            {
              distance: 12_500,
              geometry: {
                coordinates: [
                  [-74, 4],
                  [-75, 5],
                ],
              },
            },
          ],
        }),
      }),
    );
    const r = await rutaPorCarretera([
      [4, -74],
      [5, -75],
    ]);
    expect(r).toEqual({
      coords: [
        [4, -74],
        [5, -75],
      ],
      distanciaKm: 12.5,
    });
  });

  it('degrada a null si la red falla o responde mal', async () => {
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new Error('offline')));
    expect(
      await rutaPorCarretera([
        [0, 0],
        [1, 1],
      ]),
    ).toBeNull();
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue({ ok: false }));
    expect(
      await rutaPorCarretera([
        [0, 0],
        [1, 1],
      ]),
    ).toBeNull();
  });
});
