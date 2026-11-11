import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { apiInterceptor } from '../../core/http/api.interceptor';
import { LoginPage } from './login-page';

describe('LoginPage', () => {
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
    vi.spyOn(router, 'navigateByUrl').mockResolvedValue(true);
  });

  afterEach(() => controlador.verify());

  async function montar() {
    const fixture = TestBed.createComponent(LoginPage);
    fixture.detectChanges();
    await fixture.whenStable();
    const raiz = fixture.nativeElement as HTMLElement;
    const escribir = (indice: number, valor: string) => {
      const input = raiz.querySelectorAll('input')[indice];
      input.value = valor;
      input.dispatchEvent(new Event('input'));
    };
    const enviar = () =>
      raiz.querySelector('form')!.dispatchEvent(new Event('submit', { cancelable: true }));
    return { fixture, raiz, escribir, enviar };
  }

  it('muestra los campos Correo y Contraseña y el botón Ingresar', async () => {
    const { raiz } = await montar();
    const etiquetas = Array.from(raiz.querySelectorAll('label')).map((l) => l.textContent?.trim());
    expect(etiquetas).toEqual(['Correo', 'Contraseña']);
    expect(raiz.querySelector('button[type="submit"]')?.textContent?.trim()).toBe('Ingresar');
    expect(raiz.querySelector('img')?.getAttribute('alt')).toContain('SMARTANK');
  });

  it('envía las credenciales y, al autenticarse, navega a /despacho reemplazando el historial', async () => {
    const { fixture, escribir, enviar } = await montar();
    escribir(0, 'ana@smartank.co');
    escribir(1, 'secreto');
    enviar();
    fixture.detectChanges();

    const req = controlador.expectOne('/api/auth/login');
    expect(req.request.body).toEqual({ email: 'ana@smartank.co', password: 'secreto' });
    req.flush({ usuario: { id: '1', email: 'ana@smartank.co', nombre: 'Ana', rol: 'despacho' } });

    expect(router.navigateByUrl).toHaveBeenCalledWith('/despacho', { replaceUrl: true });
  });

  it('con credenciales inválidas muestra el mensaje del API y rehabilita el botón', async () => {
    const { fixture, raiz, escribir, enviar } = await montar();
    escribir(0, 'ana@smartank.co');
    escribir(1, 'mala');
    enviar();
    fixture.detectChanges();
    expect(raiz.querySelector('button[type="submit"]')?.textContent?.trim()).toBe('Ingresando…');

    controlador
      .expectOne('/api/auth/login')
      .flush(
        { code: 'UNAUTHORIZED', message: 'Credenciales inválidas', timestamp: 't' },
        { status: 401, statusText: 'Unauthorized' },
      );
    fixture.detectChanges();

    expect(raiz.querySelector('[role="alert"]')?.textContent?.trim()).toBe(
      'Credenciales inválidas',
    );
    expect((raiz.querySelector('button[type="submit"]') as HTMLButtonElement).disabled).toBe(false);
    expect(router.navigateByUrl).not.toHaveBeenCalled();
  });
});
