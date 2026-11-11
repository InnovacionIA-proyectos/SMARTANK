import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import type { AlertaApi } from '../../core/api/api-types';
import { apiInterceptor } from '../../core/http/api.interceptor';
import { AlertasPage } from './alertas-page';

const alerta = (parcial: Partial<AlertaApi>): AlertaApi => ({
  id: 'al1',
  tipo: 'desviacion_consumo_carga',
  placa: 'SNU123',
  magnitud_desviacion: 12.5,
  estado: 'nueva',
  created_at: '2026-06-17T10:30:00',
  ...parcial,
});

describe('AlertasPage', () => {
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

  async function montar(alertas: AlertaApi[]) {
    const fixture = TestBed.createComponent(AlertasPage);
    fixture.detectChanges();
    await fixture.whenStable();
    controlador
      .expectOne((r) => r.url === '/api/alertas')
      .flush({ total: alertas.length, alertas });
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    return { fixture, raiz: fixture.nativeElement as HTMLElement };
  }

  const boton = (raiz: HTMLElement, texto: string) =>
    Array.from(raiz.querySelectorAll('button')).find(
      (b) => b.textContent?.trim() === texto,
    ) as HTMLButtonElement;

  it('lista las alertas con la etiqueta legible del tipo y el estado sin guiones bajos', async () => {
    const { raiz } = await montar([
      alerta({}),
      alerta({
        id: 'al2',
        tipo: 'estacion_cara',
        estado: 'en_revision',
        magnitud_desviacion: null,
      }),
    ]);
    const filas = Array.from(raiz.querySelectorAll('tbody tr'));
    expect(filas).toHaveLength(2);
    expect(filas[0].textContent).toContain('Desviación de consumo (carga)');
    expect(filas[0].textContent).toContain('12.5');
    expect(filas[0].textContent).toContain('2026/06/17 10:30');
    expect(filas[1].textContent).toContain('Estación cara');
    expect(filas[1].textContent).toContain('en revision');
    expect(filas[1].textContent).toContain('—'); // sin magnitud
  });

  it('resalta las alertas nuevas y ofrece Revisar, Gestionar y Descartar', async () => {
    const { raiz } = await montar([alerta({})]);
    expect(raiz.querySelector('tbody tr')!.classList.contains('bg-danger/5')).toBe(true);
    expect(boton(raiz, 'Revisar')).toBeDefined();
    expect(boton(raiz, 'Gestionar')).toBeDefined();
    expect(boton(raiz, 'Descartar')).toBeDefined();
  });

  it('una alerta en revisión no ofrece Revisar y una gestionada no ofrece acciones', async () => {
    const { raiz } = await montar([
      alerta({ id: 'a', estado: 'en_revision' }),
      alerta({ id: 'b', estado: 'gestionada' }),
    ]);
    const filas = Array.from(raiz.querySelectorAll('tbody tr'));
    expect(filas[0].textContent).not.toContain('Revisar');
    expect(filas[0].textContent).toContain('Gestionar');
    expect(filas[1].querySelectorAll('button')).toHaveLength(0);
  });

  it('gestionar hace PATCH con el nuevo estado y recarga la bandeja', async () => {
    const { fixture, raiz } = await montar([alerta({ id: 'al-9' })]);
    boton(raiz, 'Gestionar').click();
    fixture.detectChanges();

    const patch = controlador.expectOne('/api/alertas/al-9');
    expect(patch.request.method).toBe('PATCH');
    expect(patch.request.body).toEqual({ estado: 'gestionada' });
    patch.flush({});
    fixture.detectChanges();
    await fixture.whenStable();

    controlador.expectOne((r) => r.url === '/api/alertas').flush({ total: 0, alertas: [] });
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(raiz.textContent).toContain('Sin alertas');
  });

  it('si el PATCH falla muestra el mensaje en la fila y no recarga', async () => {
    const { fixture, raiz } = await montar([alerta({})]);
    boton(raiz, 'Descartar').click();
    fixture.detectChanges();
    controlador
      .expectOne('/api/alertas/al1')
      .flush(
        { code: 'FORBIDDEN', message: 'No tienes permiso', timestamp: 't' },
        { status: 403, statusText: 'Forbidden' },
      );
    fixture.detectChanges();
    expect(raiz.textContent).toContain('No tienes permiso');
    expect(boton(raiz, 'Descartar').disabled).toBe(false);
  });

  it('filtrar por estado consulta el API con ese estado', async () => {
    const { fixture, raiz } = await montar([alerta({})]);
    const select = raiz.querySelector('select') as HTMLSelectElement;
    select.value = 'gestionada';
    select.dispatchEvent(new Event('change'));
    fixture.detectChanges();
    await fixture.whenStable();

    const req = controlador.expectOne((r) => r.url === '/api/alertas');
    expect(req.request.params.get('estado')).toBe('gestionada');
    req.flush({ total: 0, alertas: [] });
  });

  it('con error de carga muestra el mensaje y permite reintentar', async () => {
    const fixture: ComponentFixture<AlertasPage> = TestBed.createComponent(AlertasPage);
    fixture.detectChanges();
    await fixture.whenStable();
    controlador
      .expectOne((r) => r.url === '/api/alertas')
      .flush(
        { code: 'FORBIDDEN', message: 'Solo control de flota', timestamp: 't' },
        { status: 403, statusText: 'Forbidden' },
      );
    fixture.detectChanges();
    const raiz = fixture.nativeElement as HTMLElement;
    expect(raiz.textContent).toContain('Solo control de flota');
    boton(raiz, 'Reintentar').click();
    fixture.detectChanges();
    await fixture.whenStable();
    controlador.expectOne((r) => r.url === '/api/alertas').flush({ total: 0, alertas: [] });
  });
});
