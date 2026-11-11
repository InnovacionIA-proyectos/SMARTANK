import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import type { BitacoraOrdenApi } from '../../core/api/api-types';
import { apiInterceptor } from '../../core/http/api.interceptor';
import { ImagenService } from '../../core/servicios/imagen.service';
import { BitacoraPage } from './bitacora-page';

const ORDEN: BitacoraOrdenApi = {
  autorizacion_id: 'a1',
  placa: 'SNU123',
  manifiesto: '301457403',
  ruta: 'Bogotá - Medellín',
  estacion_nombre: 'EDS La Pintada',
  galones_max: 120,
  estado: 'autorizada',
  ya_registrada: false,
  vence_at: '2026-06-18T12:00:00Z',
};

describe('BitacoraPage', () => {
  let controlador: HttpTestingController;

  beforeEach(() => {
    URL.createObjectURL = vi.fn(() => 'blob:vista-previa');
    URL.revokeObjectURL = vi.fn();
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideHttpClient(withInterceptors([apiInterceptor])),
        provideHttpClientTesting(),
        // jsdom no decodifica imágenes ni dibuja en canvas: la compresión se simula.
        {
          provide: ImagenService,
          useValue: {
            comprimir: async () =>
              new Blob([new Uint8Array([0xff, 0xd8, 0xff])], { type: 'image/jpeg' }),
          },
        },
      ],
    });
    controlador = TestBed.inject(HttpTestingController);
  });

  afterEach(() => controlador.verify());

  async function montar(orden: Partial<BitacoraOrdenApi> | 'invalido' = {}) {
    const fixture = TestBed.createComponent(BitacoraPage);
    fixture.componentRef.setInput('token', 'tok-abc');
    fixture.detectChanges();
    await fixture.whenStable();

    const req = controlador.expectOne('/api/bitacora/tok-abc');
    if (orden === 'invalido') {
      req.flush(
        {
          code: 'BITACORA_TOKEN_INVALIDO',
          message: 'El enlace de la bitácora no es válido o venció',
          timestamp: 't',
        },
        { status: 401, statusText: 'Unauthorized' },
      );
    } else {
      req.flush({ ...ORDEN, ...orden });
    }
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const raiz = fixture.nativeElement as HTMLElement;
    const escribir = (etiqueta: string, valor: string) => {
      const label = Array.from(raiz.querySelectorAll('label')).find(
        (l) => l.textContent?.trim() === etiqueta,
      )!;
      const input = raiz.querySelector<HTMLInputElement>(`#${label.getAttribute('for')}`)!;
      input.value = valor;
      input.dispatchEvent(new Event('input'));
      fixture.detectChanges();
    };
    const enviar = () => {
      raiz.querySelector('form')!.dispatchEvent(new Event('submit', { cancelable: true }));
      fixture.detectChanges();
    };
    return { fixture, raiz, escribir, enviar };
  }

  it('muestra la orden del token (placa, estación, galones autorizados) y el formulario', async () => {
    const { raiz } = await montar();
    expect(raiz.textContent).toContain('Cierre de Tanqueo');
    expect(raiz.textContent).toContain('Manifiesto 301457403');
    expect(raiz.textContent).toContain('SNU123');
    expect(raiz.textContent).toContain('EDS La Pintada');
    expect(raiz.textContent).toContain('120');
    const etiquetas = Array.from(raiz.querySelectorAll('label')).map((l) => l.textContent?.trim());
    expect(etiquetas).toEqual(['Galones cargados', 'Odómetro (km)']);
  });

  it('con un token inválido o vencido muestra el error del API y no el formulario', async () => {
    const { raiz } = await montar('invalido');
    expect(raiz.textContent).toContain('No se pudo abrir la orden');
    expect(raiz.textContent).toContain('El enlace de la bitácora no es válido o venció');
    expect(raiz.querySelector('form')).toBeNull();
  });

  it('si la orden ya fue registrada lo informa y no permite registrar de nuevo', async () => {
    const { raiz } = await montar({ ya_registrada: true });
    expect(raiz.textContent).toContain('Tanqueo ya registrado');
    expect(raiz.querySelector('form')).toBeNull();
  });

  it('si la orden no está autorizada no muestra el formulario', async () => {
    const { raiz } = await montar({ estado: 'expirada' });
    expect(raiz.textContent).toContain('Orden no vigente');
    expect(raiz.querySelector('form')).toBeNull();
  });

  it('el botón de registrar permanece deshabilitado hasta capturar los galones', async () => {
    const { raiz, escribir } = await montar();
    const boton = raiz.querySelector('button[type="submit"]') as HTMLButtonElement;
    expect(boton.disabled).toBe(true);
    escribir('Galones cargados', '80');
    expect(boton.disabled).toBe(false);
  });

  it('registra el evento con el token y confirma al conductor', async () => {
    const { fixture, raiz, escribir, enviar } = await montar();
    escribir('Galones cargados', '80.5');
    escribir('Odómetro (km)', '123456');
    enviar();

    const req = controlador.expectOne('/api/eventos');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({
      token: 'tok-abc',
      galones_cargados: 80.5,
      odometro_evento: 123456,
      foto_surtidor_url: null,
    });
    req.flush({ id: 'e1' }, { status: 201, statusText: 'Created' });
    fixture.detectChanges();

    expect(raiz.textContent).toContain('Tanqueo registrado');
    expect(raiz.querySelector('form')).toBeNull();
  });

  it('sin odómetro envía null y muestra el error del API si ya existe un registro', async () => {
    const { fixture, raiz, escribir, enviar } = await montar();
    escribir('Galones cargados', '60');
    enviar();

    const req = controlador.expectOne('/api/eventos');
    expect(req.request.body.odometro_evento).toBeNull();
    req.flush(
      { code: 'BITACORA_YA_REGISTRADA', message: 'Esta orden ya fue registrada', timestamp: 't' },
      { status: 409, statusText: 'Conflict' },
    );
    fixture.detectChanges();

    expect(raiz.querySelector('[role="alert"]')?.textContent?.trim()).toBe(
      'Esta orden ya fue registrada',
    );
    expect((raiz.querySelector('button[type="submit"]') as HTMLButtonElement).disabled).toBe(false);
  });

  it('tras subir la foto la vista previa usa un blob local, no la URL protegida del API', async () => {
    const { fixture, raiz, escribir, enviar } = await montar();

    const archivo = raiz.querySelector('input[type="file"]') as HTMLInputElement;
    Object.defineProperty(archivo, 'files', {
      value: [new File([new Uint8Array([1, 2, 3])], 'surtidor.png', { type: 'image/png' })],
    });
    archivo.dispatchEvent(new Event('change'));

    const subida = await vi.waitFor(() => controlador.expectOne((r) => r.url === '/api/uploads'));
    expect(subida.request.params.get('token')).toBe('tok-abc');
    expect(subida.request.headers.get('Content-Type')).toBe('image/jpeg');
    subida.flush({ id: 'u1', url: '/api/uploads/u1' }, { status: 201, statusText: 'Created' });

    await vi.waitFor(() => {
      fixture.detectChanges();
      expect(raiz.querySelector('img')?.getAttribute('src')).toBe('blob:vista-previa');
    });
    expect(raiz.textContent).toContain('Imagen lista para enviar.');

    escribir('Galones cargados', '75');
    enviar();
    const evento = controlador.expectOne('/api/eventos');
    expect(evento.request.body.foto_surtidor_url).toBe('/api/uploads/u1');
    evento.flush({ id: 'e1' }, { status: 201, statusText: 'Created' });
  });
});
