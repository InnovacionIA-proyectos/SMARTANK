import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import type { AutorizacionDetalleApi } from '../../core/api/api-types';
import { apiInterceptor } from '../../core/http/api.interceptor';
import { RecomendacionPage } from './recomendacion-page';

function detalle(parcial: Partial<AutorizacionDetalleApi> = {}): AutorizacionDetalleApi {
  return {
    id: 'aut-1',
    viaje_id: 'viaje-1',
    placa: 'SNU123',
    manifiesto: '301457403',
    viaje_ruta: 'bogota-medellin',
    viaje_nombre_ruta: 'BOGOTA - MEDELLIN',
    viaje_codigo_ruta: 'BM01',
    programa: 'TM_MED-CTG SYT',
    cedula_conductor: '123',
    conductor_nombre: 'Carlos Pérez',
    estacion_id: 'est-1',
    estacion_nombre: 'EDS La Pintada',
    estacion_ruta: 'bogota-medellin',
    estacion_lat: 5.5,
    estacion_lng: -75.6,
    vehiculo_lat: 0,
    vehiculo_lng: 1,
    vehiculo_ubicacion_at: '2026-06-17T10:00:00',
    vehiculo_nivel_at: '2026-06-17T09:45:00',
    nivel_tanque_actual: 75,
    capacidad_tanque_gal: 300,
    autonomia_km: 100, // la ruta restante (200 km) supera la autonomía: requiere tanqueo
    ruta_km: 400,
    origen_lat: 0,
    origen_lng: 0,
    destino_lat: 0,
    destino_lng: 2,
    galones_max: 120,
    precio_techo: 9500,
    delta_ahorro_estimado: 150000,
    estado: 'propuesta',
    fecha_inicio: '2026-06-17T10:00:00',
    fecha_fin: '2026-06-18T10:00:00',
    ...parcial,
  };
}

describe('RecomendacionPage', () => {
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

  /** El mapa pide estaciones y ruta del viaje; se responden vacías (sin renderizar Leaflet). */
  async function resolverMapa(fixture: ComponentFixture<RecomendacionPage>) {
    fixture.detectChanges();
    await fixture.whenStable();
    controlador.expectOne((r) => r.url === '/api/estaciones').flush({ total: 0, estaciones: [] });
    controlador
      .expectOne('/api/viajes/viaje-1/ruta')
      .flush({ viaje: {}, ruta: null, vehiculo: null, autonomia_km: null });
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  }

  async function montar(datos: Partial<AutorizacionDetalleApi> = {}) {
    const fixture = TestBed.createComponent(RecomendacionPage);
    fixture.componentRef.setInput('id', 'aut-1');
    fixture.detectChanges();
    await fixture.whenStable();
    controlador.expectOne('/api/autorizaciones/aut-1').flush(detalle(datos));
    fixture.detectChanges();
    await resolverMapa(fixture);
    return { fixture, raiz: fixture.nativeElement as HTMLElement };
  }

  const boton = (raiz: HTMLElement, texto: string) =>
    Array.from(raiz.querySelectorAll('button')).find((b) => b.textContent?.trim() === texto) as
      HTMLButtonElement | undefined;

  it('sin id en la URL invita a solicitar una recomendación desde el despacho', () => {
    const fixture = TestBed.createComponent(RecomendacionPage);
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain(
      'Sin recomendación activa',
    );
    controlador.expectNone((r) => r.url.startsWith('/api/autorizaciones'));
  });

  it('muestra estación, datos del viaje y métricas de la propuesta', async () => {
    const { raiz } = await montar();
    const texto = raiz.textContent ?? '';
    expect(texto).toContain('EDS La Pintada');
    expect(texto).toContain('SNU123');
    expect(texto).toContain('301457403');
    expect(texto).toContain('Carlos Pérez');
    expect(texto).toContain('TM_MED-CTG SYT');
    expect(texto).toContain('25%'); // nivel del tanque
    expect(texto).toContain('50%'); // % recorrido
    expect(texto).toContain('75 gal'); // galones estimados
    expect(texto).toContain('400 km');
    expect(texto).toContain('100 km'); // autonomía
    expect(texto).toContain('$9.500'); // precio techo
    expect(texto).toContain('$150.000'); // ahorro estimado
    expect(texto).toContain('2026/06/17 09:45'); // última consulta de nivel
  });

  it('con propuesta que requiere tanqueo habilita Autorizar', async () => {
    const { raiz } = await montar();
    expect(boton(raiz, 'Autorizar')!.disabled).toBe(false);
    expect(raiz.textContent).not.toContain('no requiere tanqueo');
  });

  it('si la autonomía cubre la ruta restante deshabilita Autorizar y lo explica', async () => {
    const { raiz } = await montar({ autonomia_km: 300 });
    expect(boton(raiz, 'Autorizar')!.disabled).toBe(true);
    expect(raiz.textContent).toContain(
      'La autonomía (300 km) cubre la ruta restante (200 km): no requiere tanqueo.',
    );
    expect(raiz.textContent).toContain('La autonomía cubre la ruta · no requiere tanqueo');
  });

  it('autorizar publica la acción, informa el resultado y recarga el detalle sin desmontarlo', async () => {
    const { fixture, raiz } = await montar();
    boton(raiz, 'Autorizar')!.click();
    fixture.detectChanges();

    const req = controlador.expectOne('/api/autorizaciones/aut-1/autorizar');
    expect(req.request.method).toBe('POST');
    req.flush({});
    fixture.detectChanges();
    await fixture.whenStable();

    // Recarga del detalle: el contenido anterior sigue visible ("Actualizando…").
    expect(raiz.textContent).toContain('Tanqueo autorizado');
    expect(raiz.textContent).toContain('EDS La Pintada');
    controlador.expectOne('/api/autorizaciones/aut-1').flush(detalle({ estado: 'autorizada' }));
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(boton(raiz, 'Autorizar')).toBeUndefined();
    expect(boton(raiz, 'Pre-autorizar EDS')).toBeDefined();
    expect(boton(raiz, 'Notificar conductor')).toBeDefined();
  });

  it('notificar sin WhatsApp enviado muestra el motivo como error', async () => {
    const { fixture, raiz } = await montar({ estado: 'autorizada' });
    boton(raiz, 'Notificar conductor')!.click();
    fixture.detectChanges();

    controlador.expectOne('/api/autorizaciones/aut-1/notificar').flush({
      bitacora_url: 'https://app/bitacora/tok',
      destinatario: { nombre: 'Carlos', telefono: '+57300' },
      whatsapp: { enviado: false, motivo: 'WhatsApp no configurado' },
    });
    fixture.detectChanges();

    const alerta = raiz.querySelector('[role="alert"]');
    expect(alerta?.textContent?.trim()).toBe('WhatsApp no enviado: WhatsApp no configurado');
    expect(alerta?.className).toContain('text-danger');
  });

  it('notificar con envío exitoso confirma y pre-autorizar registra en la EDS', async () => {
    const { fixture, raiz } = await montar({ estado: 'autorizada' });

    boton(raiz, 'Pre-autorizar EDS')!.click();
    fixture.detectChanges();
    controlador
      .expectOne('/api/autorizaciones/aut-1/preautorizar-eds')
      .flush({ autorizacion: {}, eds: { simulado: true } });
    fixture.detectChanges();
    await fixture.whenStable();
    expect(raiz.textContent).toContain('Pre-autorización registrada en la EDS');
    controlador.expectOne('/api/autorizaciones/aut-1').flush(detalle({ estado: 'autorizada' }));
    fixture.detectChanges();
    await fixture.whenStable();

    boton(raiz, 'Notificar conductor')!.click();
    fixture.detectChanges();
    controlador.expectOne('/api/autorizaciones/aut-1/notificar').flush({
      bitacora_url: 'https://app/bitacora/tok',
      destinatario: { nombre: 'Carlos', telefono: '+57300' },
      whatsapp: { enviado: true, sid: 'SM1' },
    });
    fixture.detectChanges();
    await fixture.whenStable();
    expect(raiz.textContent).toContain('Mensaje enviado por Whatsapp');
    controlador.expectOne('/api/autorizaciones/aut-1').flush(detalle({ estado: 'autorizada' }));
  });

  it('si autorizar falla muestra el mensaje del API y no recarga', async () => {
    const { fixture, raiz } = await montar();
    boton(raiz, 'Autorizar')!.click();
    fixture.detectChanges();
    controlador
      .expectOne('/api/autorizaciones/aut-1/autorizar')
      .flush(
        { code: 'CONFLICTO', message: 'Solo se puede autorizar una propuesta', timestamp: 't' },
        { status: 409, statusText: 'Conflict' },
      );
    fixture.detectChanges();
    expect(raiz.querySelector('[role="alert"]')?.textContent?.trim()).toBe(
      'Solo se puede autorizar una propuesta',
    );
    controlador.expectNone('/api/autorizaciones/aut-1');
  });

  it('un error en la carga inicial muestra el mensaje y permite reintentar', async () => {
    const fixture = TestBed.createComponent(RecomendacionPage);
    fixture.componentRef.setInput('id', 'aut-1');
    fixture.detectChanges();
    await fixture.whenStable();
    controlador
      .expectOne('/api/autorizaciones/aut-1')
      .flush(
        { code: 'NOT_FOUND', message: 'Autorización no encontrada', timestamp: 't' },
        { status: 404, statusText: 'Not Found' },
      );
    fixture.detectChanges();

    const raiz = fixture.nativeElement as HTMLElement;
    expect(raiz.textContent).toContain('Autorización no encontrada');
    boton(raiz, 'Reintentar')!.click();
    fixture.detectChanges();
    await fixture.whenStable();
    controlador.expectOne('/api/autorizaciones/aut-1').flush(detalle());
    fixture.detectChanges();
    await resolverMapa(fixture);
    expect(raiz.textContent).toContain('EDS La Pintada');
  });
});
