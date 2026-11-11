import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import type { ViajeApi, ViajesResponse } from '../../core/api/api-types';
import { apiInterceptor } from '../../core/http/api.interceptor';
import { DespachoPage } from './despacho-page';

function viaje(parcial: Partial<ViajeApi>): ViajeApi {
  return {
    id: 'v1',
    vehiculo_id: 'h1',
    placa: 'SNU123',
    tipo_motor: 'diesel',
    ruta: 'bogota-medellin',
    nombre_ruta: 'BOGOTA - MEDELLIN',
    codigo_ruta: 'BM01',
    estado: 'en_ruta',
    autonomia_estimada_km: null,
    nivel_tanque_actual: 150,
    capacidad_tanque_gal: 300,
    ubicacion_at: null,
    nivel_at: '2026-06-17T10:30:00',
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
    recomendacion_activa: false,
    ...parcial,
  };
}

describe('DespachoPage', () => {
  let controlador: HttpTestingController;
  let router: Router;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideHttpClient(withInterceptors([apiInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    controlador = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
    vi.spyOn(router, 'navigate').mockResolvedValue(true);
  });

  afterEach(() => controlador.verify());

  const respuesta = (viajes: ViajeApi[]): ViajesResponse => ({
    total: viajes.length,
    viajes,
    opciones: { rutas: ['BOGOTA - MEDELLIN'], anios: [2026] },
  });

  async function montar(viajes: ViajeApi[]) {
    const fixture = TestBed.createComponent(DespachoPage);
    fixture.detectChanges();
    await fixture.whenStable();
    const req = controlador.expectOne((r) => r.url === '/api/viajes');
    req.flush(respuesta(viajes));
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    return { fixture, raiz: fixture.nativeElement as HTMLElement, req };
  }

  const placas = (raiz: HTMLElement) =>
    Array.from(raiz.querySelectorAll('tbody tr')).map((tr) =>
      tr.querySelector('td')?.textContent?.trim(),
    );

  async function cambiarSelect(
    fixture: ComponentFixture<DespachoPage>,
    etiqueta: string,
    valor: string,
  ) {
    const raiz = fixture.nativeElement as HTMLElement;
    const label = Array.from(raiz.querySelectorAll('label')).find(
      (l) => l.textContent?.trim() === etiqueta,
    )!;
    const select = raiz.querySelector<HTMLSelectElement>(`#${label.getAttribute('for')}`)!;
    select.value = valor;
    select.dispatchEvent(new Event('change'));
    fixture.detectChanges();
    await fixture.whenStable();
  }

  it('consulta los viajes con los filtros por defecto (en ruta, mes y año actuales, cargados)', async () => {
    const { req } = await montar([viaje({})]);
    const hoy = new Date();
    expect(req.request.params.get('estado')).toBe('en_ruta');
    expect(req.request.params.get('anio')).toBe(String(hoy.getFullYear()));
    expect(req.request.params.get('mes')).toBe(String(hoy.getMonth() + 1));
    expect(req.request.params.get('desplazamiento')).toBe('cargado');
    expect(req.request.params.has('placa')).toBe(false);
    expect(req.request.params.has('telemetria')).toBe(false);
  });

  it('por defecto solo lista los viajes que necesitan tanqueo y los ordena por inicio ascendente', async () => {
    const { raiz } = await montar([
      viaje({ id: 'a', placa: 'TARDE1', inicio_at: '2026-06-17T12:00:00', autonomia_km: 100 }),
      viaje({ id: 'b', placa: 'ALCANZA', autonomia_km: 350 }), // restante 200 km: no necesita
      viaje({ id: 'c', placa: 'PRIMERO', inicio_at: '2026-06-16T06:00:00', autonomia_km: 100 }),
      viaje({ id: 'd', placa: 'SINDATO', autonomia_km: null, autonomia_estimada_km: null }), // indeterminado
    ]);
    expect(placas(raiz)).toEqual(['PRIMERO', 'TARDE1']);
  });

  it('al elegir "Todos" en Necesita tanqueo aparecen todos los viajes', async () => {
    const { fixture, raiz } = await montar([
      viaje({ id: 'a', placa: 'NECESITA', autonomia_km: 100 }),
      viaje({ id: 'b', placa: 'ALCANZA', autonomia_km: 350 }),
    ]);
    expect(placas(raiz)).toEqual(['NECESITA']);

    await cambiarSelect(fixture, 'Necesita tanqueo', '');
    expect(placas(raiz).sort()).toEqual(['ALCANZA', 'NECESITA']);
    controlador.expectNone((r) => r.url === '/api/viajes'); // es un filtro de cliente: no recarga
  });

  it('un filtro de servidor (estado) dispara una nueva consulta con el valor elegido', async () => {
    const { fixture } = await montar([viaje({})]);
    await cambiarSelect(fixture, 'Estado', 'programado');
    const nueva = controlador.expectOne((r) => r.url === '/api/viajes');
    expect(nueva.request.params.get('estado')).toBe('programado');
    nueva.flush(respuesta([]));
  });

  it('muestra los valores derivados: nivel %, galones estimados, % recorrido y autonomía', async () => {
    const { raiz } = await montar([viaje({ autonomia_km: 100 })]);
    const celdas = Array.from(raiz.querySelectorAll('tbody tr td')).map((td) =>
      td.textContent?.trim(),
    );
    expect(celdas).toContain('50%'); // nivel del tanque y % recorrido (ambos 50)
    expect(celdas).toContain('150'); // galones estimados
    expect(celdas).toContain('BOGOTA - MEDELLIN');
    expect(celdas).toContain('2026/06/17 10:30');
  });

  it('mientras recarga conserva la tabla visible y avisa "Actualizando…"', async () => {
    const { fixture, raiz } = await montar([viaje({ autonomia_km: 100 })]);
    await cambiarSelect(fixture, 'Estado', 'programado');
    expect(raiz.textContent).toContain('Actualizando…');
    expect(placas(raiz)).toEqual(['SNU123']);
    controlador.expectOne((r) => r.url === '/api/viajes').flush(respuesta([]));
  });

  it('Recomendar EDS genera la propuesta y abre la pantalla de recomendación con su id', async () => {
    const { fixture, raiz } = await montar([viaje({ id: 'viaje-7', autonomia_km: 100 })]);
    const boton = Array.from(raiz.querySelectorAll('button')).find(
      (b) => b.textContent?.trim() === 'Recomendar EDS',
    )!;
    boton.click();
    fixture.detectChanges();

    const req = controlador.expectOne('/api/viajes/viaje-7/recomendacion');
    expect(req.request.method).toBe('POST');
    req.flush({ requiere_tanqueo: true, autorizacion: { id: 'aut-9' } });

    expect(router.navigate).toHaveBeenCalledWith(['/recomendacion'], {
      queryParams: { id: 'aut-9' },
    });
  });

  it('Actualizar Autonomía recalcula y recarga la lista', async () => {
    const { fixture, raiz } = await montar([viaje({ id: 'viaje-7', autonomia_km: 100 })]);
    const boton = Array.from(raiz.querySelectorAll('button')).find(
      (b) => b.textContent?.trim() === 'Actualizar Autonomía',
    )!;
    boton.click();
    fixture.detectChanges();

    controlador.expectOne('/api/viajes/viaje-7/autonomia').flush({ autonomia_km: 210 });
    fixture.detectChanges();
    await fixture.whenStable();

    controlador.expectOne((r) => r.url === '/api/viajes').flush(respuesta([]));
  });

  it('si el API rechaza la acción muestra el mensaje en la fila', async () => {
    const { fixture, raiz } = await montar([viaje({ id: 'viaje-7', autonomia_km: 100 })]);
    const boton = Array.from(raiz.querySelectorAll('button')).find(
      (b) => b.textContent?.trim() === 'Recomendar EDS',
    )!;
    boton.click();
    fixture.detectChanges();

    controlador.expectOne('/api/viajes/viaje-7/recomendacion').flush(
      {
        code: 'TELEMETRIA_OBSOLETA',
        message: 'La telemetría del tanque es muy antigua',
        timestamp: 't',
      },
      { status: 409, statusText: 'Conflict' },
    );
    fixture.detectChanges();

    expect(raiz.textContent).toContain('La telemetría del tanque es muy antigua');
    expect(router.navigate).not.toHaveBeenCalled();
  });

  it('con error de carga inicial muestra el mensaje y permite reintentar', async () => {
    const fixture = TestBed.createComponent(DespachoPage);
    fixture.detectChanges();
    await fixture.whenStable();
    controlador
      .expectOne((r) => r.url === '/api/viajes')
      .flush(
        { code: 'X', message: 'Servicio caído', timestamp: 't' },
        { status: 503, statusText: 'Unavailable' },
      );
    fixture.detectChanges();

    const raiz = fixture.nativeElement as HTMLElement;
    expect(raiz.textContent).toContain('Servicio caído');
    Array.from(raiz.querySelectorAll('button'))
      .find((b) => b.textContent?.trim() === 'Reintentar')!
      .click();
    fixture.detectChanges();
    await fixture.whenStable();
    controlador.expectOne((r) => r.url === '/api/viajes').flush(respuesta([]));
  });
});
