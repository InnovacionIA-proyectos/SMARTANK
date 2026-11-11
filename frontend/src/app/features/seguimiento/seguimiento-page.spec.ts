import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import type { ViajeApi } from '../../core/api/api-types';
import { apiInterceptor } from '../../core/http/api.interceptor';
import { SeguimientoPage } from './seguimiento-page';

function viaje(parcial: Partial<ViajeApi>): ViajeApi {
  return {
    id: 'v1',
    vehiculo_id: 'h1',
    placa: 'SNU123',
    tipo_motor: 'diesel',
    ruta: 'bogota-medellin',
    nombre_ruta: null,
    codigo_ruta: null,
    estado: 'en_ruta',
    autonomia_estimada_km: null,
    nivel_tanque_actual: 100,
    capacidad_tanque_gal: 300,
    ubicacion_at: null,
    nivel_at: null,
    autonomia_km: 150,
    ruta_km: 400,
    vehiculo_lat: 0,
    vehiculo_lng: 1,
    origen_lat: 0,
    origen_lng: 0,
    destino_lat: 0,
    destino_lng: 2,
    manifiesto: '301457403',
    inicio_at: '2026-06-17T08:00:00',
    fecha_despacho: null,
    recomendacion_activa: true,
    ...parcial,
  };
}

describe('SeguimientoPage', () => {
  let controlador: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideHttpClient(withInterceptors([apiInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    controlador = TestBed.inject(HttpTestingController);
  });

  afterEach(() => controlador.verify());

  async function montar(viajes: ViajeApi[]) {
    const fixture = TestBed.createComponent(SeguimientoPage);
    fixture.detectChanges();
    await fixture.whenStable();
    const req = controlador.expectOne((r) => r.url === '/api/viajes');
    req.flush({
      total: viajes.length,
      viajes,
      opciones: { rutas: ['BOGOTA - MEDELLIN'], anios: [2026] },
    });
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    return { fixture, raiz: fixture.nativeElement as HTMLElement, req };
  }

  it('consulta solo viajes con recomendación activa y cargados por defecto', async () => {
    const { req } = await montar([viaje({})]);
    expect(req.request.params.get('con_recomendacion')).toBe('1');
    expect(req.request.params.get('desplazamiento')).toBe('cargado');
    expect(req.request.params.has('estado')).toBe(false);
  });

  it('calcula los KPIs: total, crítico (<100), precaución (100-200) y promedio', async () => {
    const { raiz } = await montar([
      viaje({ id: 'a', autonomia_km: 50 }),
      viaje({ id: 'b', autonomia_km: 150 }),
      viaje({ id: 'c', autonomia_km: 250 }),
      viaje({ id: 'd', autonomia_km: null, autonomia_estimada_km: 100 }),
    ]);
    const kpis = Array.from(raiz.querySelectorAll('app-kpi')).map((k) =>
      k.textContent?.replace(/\s+/g, ' ').trim(),
    );
    expect(kpis[0]).toBe('Vehículos en seguimiento4');
    expect(kpis[1]).toBe('Crítico (< 100 km)1');
    expect(kpis[2]).toBe('Precaución (100–200 km)2');
    expect(kpis[3]).toBe('Autonomía promedio138 km'); // (50+150+250+100)/4 = 137,5 -> 138
  });

  it('ordena por autonomía ascendente, pinta el semáforo y marca las filas críticas', async () => {
    const { raiz } = await montar([
      viaje({ id: 'a', placa: 'VERDE', autonomia_km: 250 }),
      viaje({ id: 'b', placa: 'ROJO', autonomia_km: 40 }),
      viaje({ id: 'c', placa: 'AMARILLO', autonomia_km: 150 }),
    ]);
    const filas = Array.from(raiz.querySelectorAll('tbody tr'));
    expect(filas.map((f) => f.querySelector('td')?.textContent?.trim())).toEqual([
      'ROJO',
      'AMARILLO',
      'VERDE',
    ]);
    expect(filas[0].classList.contains('bg-danger/5')).toBe(true);
    expect(filas[1].classList.contains('bg-danger/5')).toBe(false);
    expect(Array.from(filas[0].querySelectorAll('app-badge')).at(-1)!.className).toContain(
      'text-danger',
    );
    expect(
      Array.from(filas[1].querySelectorAll('app-badge')).at(-1)!.className.includes('text-warning')
        ? document.body
        : null,
    ).not.toBeNull();
    expect(
      Array.from(filas[2].querySelectorAll('app-badge')).at(-1)!.className.includes('text-success')
        ? document.body
        : null,
    ).not.toBeNull();
  });

  it('el estado se muestra sin guiones bajos', async () => {
    const { raiz } = await montar([viaje({ estado: 'en_ruta' })]);
    expect(raiz.querySelector('tbody')!.textContent).toContain('en ruta');
  });

  it('Ver ruta abre el panel, carga la ruta del viaje y permite cerrarlo', async () => {
    const { fixture, raiz } = await montar([viaje({ id: 'viaje-5', placa: 'SNU123' })]);
    (
      Array.from(raiz.querySelectorAll('button')).find(
        (b) => b.textContent?.trim() === 'Ver ruta',
      ) as HTMLButtonElement
    ).click();
    fixture.detectChanges();
    await fixture.whenStable();

    controlador.expectOne('/api/viajes/viaje-5/ruta').flush({
      viaje: { id: 'viaje-5', ruta: 'bogota-medellin', estado: 'en_ruta', placa: 'SNU123' },
      ruta: null,
      vehiculo: null,
      autonomia_km: null,
    });
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(raiz.textContent).toContain('Ruta y autonomía · SNU123');
    expect(raiz.textContent).toContain('Este viaje no tiene ruta calculada (Valhalla)');

    (
      Array.from(raiz.querySelectorAll('button')).find(
        (b) => b.textContent?.trim() === 'Cerrar',
      ) as HTMLButtonElement
    ).click();
    fixture.detectChanges();
    expect(raiz.textContent).not.toContain('Ruta y autonomía ·');
  });

  it('sin viajes muestra el estado vacío específico', async () => {
    const { raiz } = await montar([]);
    expect(raiz.textContent).toContain('Sin viajes con recomendación activa');
  });
});
