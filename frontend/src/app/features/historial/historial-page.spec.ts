import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import type { EventoTanqueoApi } from '../../core/api/api-types';
import { apiInterceptor } from '../../core/http/api.interceptor';
import { HistorialPage } from './historial-page';

const evento = (parcial: Partial<EventoTanqueoApi>): EventoTanqueoApi => ({
  id: 'e1',
  placa: 'SNU123',
  tipo_motor: 'diesel',
  programa: 'TM_MED-CTG SYT',
  estacion_nombre: 'EDS La Pintada',
  estacion_ruta: 'Bogotá',
  proveedor: 'terpel',
  galones_cargados: 80.25,
  precio_pagado: 9500,
  odometro_evento: 1234567,
  autorizado: true,
  precio_techo: 9800,
  foto_surtidor_url: '/api/uploads/abc',
  nivel_antes: 10,
  nivel_despues: 90,
  ocurrido_at: '2026-06-17T10:30:00',
  ...parcial,
});

describe('HistorialPage', () => {
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

  async function montar(eventos: EventoTanqueoApi[]) {
    const fixture = TestBed.createComponent(HistorialPage);
    fixture.detectChanges();
    await fixture.whenStable();
    const inicial = controlador.expectOne((r) => r.url === '/api/eventos');
    expect(inicial.request.params.keys()).toEqual([]);
    inicial.flush({ total: eventos.length, eventos });
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    return { fixture, raiz: fixture.nativeElement as HTMLElement };
  }

  const escribir = (fixture: ComponentFixture<HistorialPage>, etiqueta: string, valor: string) => {
    const raiz = fixture.nativeElement as HTMLElement;
    const label = Array.from(raiz.querySelectorAll('label')).find(
      (l) => l.textContent?.trim() === etiqueta,
    )!;
    const control = raiz.querySelector<HTMLInputElement | HTMLSelectElement>(
      `#${label.getAttribute('for')}`,
    )!;
    control.value = valor;
    control.dispatchEvent(new Event(control instanceof HTMLSelectElement ? 'change' : 'input'));
    fixture.detectChanges();
  };

  it('muestra los tanqueos con proveedor, galones con un decimal y precio en pesos', async () => {
    const { raiz } = await montar([
      evento({}),
      evento({ id: 'e2', proveedor: null, programa: null, autorizado: false }),
    ]);
    const filas = Array.from(raiz.querySelectorAll('tbody tr'));
    expect(filas).toHaveLength(2);
    expect(filas[0].textContent).toContain('2026/06/17 10:30');
    expect(filas[0].textContent).toContain('Terpel');
    expect(filas[0].textContent).toContain('80.3'); // 80.25 -> toFixed(1)
    expect(filas[0].textContent).toMatch(/\$\s?9\.500/);
    expect(filas[0].textContent).toContain('1.234.567');
    expect(filas[0].textContent).toContain('Sí');
    expect(filas[1].textContent).toContain('No');
  });

  it('los filtros no consultan hasta pulsar Buscar y se envían sin vacíos', async () => {
    const { fixture } = await montar([evento({})]);
    escribir(fixture, 'Placa', '  SNU123 ');
    escribir(fixture, 'Proveedor', 'distracom');
    escribir(fixture, 'Desde', '2026-06-01');
    controlador.expectNone((r) => r.url === '/api/eventos');

    (fixture.nativeElement as HTMLElement)
      .querySelector('form')!
      .dispatchEvent(new Event('submit', { cancelable: true }));
    fixture.detectChanges();
    await fixture.whenStable();

    const req = controlador.expectOne((r) => r.url === '/api/eventos');
    expect(req.request.params.get('placa')).toBe('SNU123');
    expect(req.request.params.get('proveedor')).toBe('distracom');
    expect(req.request.params.get('desde')).toBe('2026-06-01');
    expect(req.request.params.has('hasta')).toBe(false);
    req.flush({ total: 0, eventos: [] });
  });

  it('Limpiar borra los filtros y vuelve a consultar sin parámetros', async () => {
    const { fixture, raiz } = await montar([evento({})]);
    escribir(fixture, 'Placa', 'XYZ999');
    (raiz.querySelector('form') as HTMLFormElement).dispatchEvent(
      new Event('submit', { cancelable: true }),
    );
    fixture.detectChanges();
    await fixture.whenStable();
    controlador.expectOne((r) => r.url === '/api/eventos').flush({ total: 0, eventos: [] });
    fixture.detectChanges();

    const limpiar = Array.from(raiz.querySelectorAll('button')).find(
      (b) => b.textContent?.trim() === 'Limpiar',
    )!;
    limpiar.click();
    fixture.detectChanges();
    await fixture.whenStable();
    const req = controlador.expectOne((r) => r.url === '/api/eventos');
    expect(req.request.params.keys()).toEqual([]);
    req.flush({ total: 0, eventos: [] });
  });

  it('Ver abre la bitácora con los datos del conductor y el enlace a la foto', async () => {
    const { fixture, raiz } = await montar([evento({})]);
    expect(raiz.querySelector('[role="dialog"]')).toBeNull();

    (
      Array.from(raiz.querySelectorAll('tbody button')).find(
        (b) => b.textContent?.trim() === 'Ver',
      ) as HTMLButtonElement
    ).click();
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const dialogo = raiz.querySelector('[role="dialog"]') as HTMLElement;
    expect(dialogo.getAttribute('aria-label')).toBe('Bitácora del tanqueo');
    expect(dialogo.textContent).toContain('Nivel antes');
    expect(dialogo.textContent).toContain('10 gal');
    expect(dialogo.textContent).toContain('90 gal');
    expect(dialogo.textContent).toContain('1.234.567 km');
    const enlace = dialogo.querySelector('a') as HTMLAnchorElement;
    expect(enlace.getAttribute('href')).toBe('/api/uploads/abc');
    expect(enlace.getAttribute('rel')).toBe('noopener noreferrer');

    (dialogo.querySelector('[aria-label="Cerrar"]') as HTMLButtonElement).click();
    fixture.detectChanges();
    expect(raiz.querySelector('[role="dialog"]')).toBeNull();
  });

  it('sin foto indica "Sin foto"', async () => {
    const { fixture, raiz } = await montar([evento({ foto_surtidor_url: null })]);
    (
      Array.from(raiz.querySelectorAll('tbody button')).find(
        (b) => b.textContent?.trim() === 'Ver',
      ) as HTMLButtonElement
    ).click();
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(raiz.querySelector('[role="dialog"]')!.textContent).toContain('Sin foto');
  });

  it('sin resultados muestra el estado vacío', async () => {
    const { raiz } = await montar([]);
    expect(raiz.textContent).toContain('Sin tanqueos');
    expect(raiz.querySelector('table')).toBeNull();
  });
});
