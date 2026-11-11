import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import type { MetricasResponse } from '../../core/api/api-types';
import { apiInterceptor } from '../../core/http/api.interceptor';
import { TableroPage } from './tablero-page';

const metricas = (parcial: Partial<MetricasResponse> = {}): MetricasResponse => ({
  filtros: { programa: null, proveedor: null, producto: null, anio: 2026, mes: 6, ruta: null },
  galones_total: 12345.5,
  tanqueos_total: 42,
  gasto_real: 1_043_000_000,
  gasto_ideal: 980_000_000,
  gasto_maximo: 1_100_000_000,
  ahorro_vs_maximo: 57_000_000,
  oportunidad_vs_ideal: 63_000_000,
  precio_minimo_ruta: 9100,
  // Sin datos de proveedor/mapas: el gráfico y los mapas muestran su estado vacío.
  utilizacion_eds: [],
  equipos: [
    {
      equipo: 'SNU123',
      mes: 6,
      fecha: '2026-06-17T10:30:00',
      valor_galon: 9500,
      galones: 80.25,
      valor_total: 762_000,
      eds: 'EDS La Pintada',
      ciudad: null,
    },
  ],
  galones_por_eds: [],
  rutas: [],
  opciones: {
    programas: ['TM_MED-CTG SYT'],
    proveedores: ['terpel', 'distracom'],
    productos: ['diesel'],
    anios: [2025, 2026],
    meses: [5, 6],
    rutas: ['bogota-medellin'],
  },
  ...parcial,
});

describe('TableroPage', () => {
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

  async function montar(datos: MetricasResponse) {
    const fixture = TestBed.createComponent(TableroPage);
    fixture.detectChanges();
    await fixture.whenStable();
    const req = controlador.expectOne((r) => r.url === '/api/metricas/recuperacion');
    req.flush(datos);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    return { fixture, raiz: fixture.nativeElement as HTMLElement, req };
  }

  it('consulta por defecto el mes y año actuales', async () => {
    const { req } = await montar(metricas());
    const hoy = new Date();
    expect(req.request.params.get('anio')).toBe(String(hoy.getFullYear()));
    expect(req.request.params.get('mes')).toBe(String(hoy.getMonth() + 1));
    expect(req.request.params.has('programa')).toBe(false);
  });

  it('muestra los KPIs en formato corto de pesos y el ahorro destacado', async () => {
    const { raiz } = await montar(metricas());
    const texto = (raiz.textContent ?? '').replace(/\s+/g, ' ');
    expect(texto).toContain('Gasto real (pagado)$1.043 mill.');
    expect(texto).toContain('12.345,5 gal · 42 tanqueos');
    expect(texto).toContain('Precio mín. ruta: $9.100/gal');
    expect(texto).toContain('Gasto máximo (estación más cara)$1.100 mill.');
    expect(texto).toContain('Ahorro vs. estación más cara$57.000.000');
    expect(texto).toContain('Oportunidad vs. ideal: $63.000.000');
    const ahorro = Array.from(raiz.querySelectorAll('app-kpi')).at(-1)!;
    expect(ahorro.querySelector('app-card')!.className).toContain('border-success');
  });

  it('los selects usan las opciones del servidor con mes en español y proveedor capitalizado', async () => {
    const { raiz } = await montar(metricas());
    const opciones = (etiqueta: string) => {
      const label = Array.from(raiz.querySelectorAll('label')).find(
        (l) => l.textContent?.trim() === etiqueta,
      )!;
      return Array.from(
        raiz.querySelectorAll<HTMLOptionElement>(`#${label.getAttribute('for')} option`),
      ).map((o) => o.textContent?.trim());
    };
    expect(opciones('Mes')).toEqual(['Todos', 'Mayo', 'Junio']);
    expect(opciones('Proveedor')).toEqual(['Todos', 'Terpel', 'Distracom']);
    expect(opciones('Año')).toEqual(['Todos', '2025', '2026']);
    expect(opciones('Ruta')).toEqual(['Todas', 'bogota-medellin']);
  });

  it('cambiar un filtro consulta de nuevo y conserva la vista mientras recarga', async () => {
    const { fixture, raiz } = await montar(metricas());
    const label = Array.from(raiz.querySelectorAll('label')).find(
      (l) => l.textContent?.trim() === 'Proveedor',
    )!;
    const select = raiz.querySelector<HTMLSelectElement>(`#${label.getAttribute('for')}`)!;
    select.value = 'terpel';
    select.dispatchEvent(new Event('change'));
    fixture.detectChanges();
    await fixture.whenStable();

    expect(raiz.textContent).toContain('Actualizando…');
    expect(raiz.textContent).toContain('Gasto real (pagado)');
    const req = controlador.expectOne((r) => r.url === '/api/metricas/recuperacion');
    expect(req.request.params.get('proveedor')).toBe('terpel');
    req.flush(metricas());
  });

  it('el historial de tanqueos usa mes en español, galones con un decimal y pesos', async () => {
    const { raiz } = await montar(metricas());
    const fila = raiz.querySelector('tbody tr')!;
    const celdas = Array.from(fila.querySelectorAll('td')).map((td) => td.textContent?.trim());
    expect(celdas).toEqual([
      '2026/06/17 10:30',
      'SNU123',
      'Junio',
      '80,3',
      '$9.500',
      '$762.000',
      'EDS La Pintada',
      '—',
    ]);
  });

  it('sin tanqueos en el filtro muestra el estado vacío pero conserva los filtros', async () => {
    const { raiz } = await montar(metricas({ tanqueos_total: 0 }));
    expect(raiz.textContent).toContain('Sin tanqueos en el filtro');
    expect(raiz.querySelectorAll('select')).toHaveLength(6);
    expect(raiz.querySelector('app-kpi')).toBeNull();
  });

  it('con error de carga inicial muestra el mensaje', async () => {
    const fixture = TestBed.createComponent(TableroPage);
    fixture.detectChanges();
    await fixture.whenStable();
    controlador
      .expectOne((r) => r.url === '/api/metricas/recuperacion')
      .flush(
        { code: 'FORBIDDEN', message: 'Solo finanzas', timestamp: 't' },
        { status: 403, statusText: 'Forbidden' },
      );
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Solo finanzas');
  });
});
