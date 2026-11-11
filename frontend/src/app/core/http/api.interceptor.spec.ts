import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { ApiError } from '../api/api-error';
import { AuthService } from '../auth/auth.service';
import { apiInterceptor } from './api.interceptor';

describe('apiInterceptor', () => {
  let http: HttpClient;
  let controlador: HttpTestingController;
  let router: Router;
  let auth: AuthService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideHttpClient(withInterceptors([apiInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpClient);
    controlador = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
    auth = TestBed.inject(AuthService);
    vi.spyOn(router, 'navigateByUrl').mockResolvedValue(true);
  });

  afterEach(() => controlador.verify());

  const capturarError = (url: string): ApiError[] => {
    const errores: ApiError[] = [];
    http.get(url).subscribe({ error: (e: ApiError) => errores.push(e) });
    return errores;
  };

  it('envía las credenciales (cookie de sesión) en las rutas /api', () => {
    http.get('/api/viajes').subscribe();
    const req = controlador.expectOne('/api/viajes');
    expect(req.request.withCredentials).toBe(true);
    req.flush({});
  });

  it('no toca peticiones que no son del API', () => {
    http.get('/assets/datos.json').subscribe();
    const req = controlador.expectOne('/assets/datos.json');
    expect(req.request.withCredentials).toBe(false);
    req.flush({});
  });

  it('convierte el error del API ({ code, message, timestamp, details }) en ApiError', () => {
    const errores = capturarError('/api/alertas');
    controlador.expectOne('/api/alertas').flush(
      {
        code: 'FORBIDDEN',
        message: 'No autorizado para esta acción',
        timestamp: 't',
        details: [1],
      },
      { status: 403, statusText: 'Forbidden' },
    );
    expect(errores).toHaveLength(1);
    expect(errores[0]).toBeInstanceOf(ApiError);
    expect(errores[0].message).toBe('No autorizado para esta acción');
    expect(errores[0].code).toBe('FORBIDDEN');
    expect(errores[0].status).toBe(403);
    expect(errores[0].details).toEqual([1]);
  });

  it('un fallo de red (status 0) produce el mensaje de conexión', () => {
    const errores = capturarError('/api/viajes');
    controlador.expectOne('/api/viajes').error(new ProgressEvent('error'));
    expect(errores[0].message).toBe('Error de conexión con el servidor');
    expect(errores[0].status).toBe(0);
  });

  it('ante un 401 limpia la sesión y navega a /login', () => {
    auth.login({ email: 'a@b.co', password: 'x' }).subscribe();
    controlador.expectOne('/api/auth/login').flush({
      usuario: { id: '1', email: 'a@b.co', nombre: 'Ana', rol: 'admin' },
    });
    expect(auth.estaAutenticado()).toBe(true);

    capturarError('/api/viajes');
    controlador
      .expectOne('/api/viajes')
      .flush(
        { code: 'UNAUTHORIZED', message: 'Sesión expirada', timestamp: 't' },
        { status: 401, statusText: 'Unauthorized' },
      );

    expect(auth.estaAutenticado()).toBe(false);
    expect(router.navigateByUrl).toHaveBeenCalledWith('/login', { replaceUrl: true });
  });

  it('un 401 de los endpoints de autenticación no redirige (credenciales inválidas)', () => {
    const errores: ApiError[] = [];
    auth
      .login({ email: 'a@b.co', password: 'mala' })
      .subscribe({ error: (e: ApiError) => errores.push(e) });
    controlador
      .expectOne('/api/auth/login')
      .flush(
        { code: 'UNAUTHORIZED', message: 'Credenciales inválidas', timestamp: 't' },
        { status: 401, statusText: 'Unauthorized' },
      );
    expect(errores[0].message).toBe('Credenciales inválidas');
    expect(router.navigateByUrl).not.toHaveBeenCalled();
  });

  it('un 401 en la bitácora pública (token vencido) no empuja al login', () => {
    const errores = capturarError('/api/bitacora/token-vencido');
    controlador
      .expectOne('/api/bitacora/token-vencido')
      .flush(
        { code: 'BITACORA_TOKEN_INVALIDO', message: 'El enlace venció', timestamp: 't' },
        { status: 401, statusText: 'Unauthorized' },
      );
    expect(errores[0].code).toBe('BITACORA_TOKEN_INVALIDO');
    expect(router.navigateByUrl).not.toHaveBeenCalled();
  });

  it('un 401 al subir la foto o registrar el evento con token tampoco redirige', () => {
    http
      .post('/api/uploads?token=vencido', new Blob([new Uint8Array([1])]))
      .subscribe({ error: () => undefined });
    controlador
      .expectOne('/api/uploads?token=vencido')
      .flush(
        { code: 'BITACORA_TOKEN_INVALIDO', message: 'x', timestamp: 't' },
        { status: 401, statusText: 'Unauthorized' },
      );

    http
      .post('/api/eventos', { token: 'vencido', galones_cargados: 1 })
      .subscribe({ error: () => undefined });
    controlador
      .expectOne('/api/eventos')
      .flush(
        { code: 'BITACORA_TOKEN_INVALIDO', message: 'x', timestamp: 't' },
        { status: 401, statusText: 'Unauthorized' },
      );

    expect(router.navigateByUrl).not.toHaveBeenCalled();
  });

  it('el historial (GET /api/eventos) sí exige sesión: un 401 redirige al login', () => {
    capturarError('/api/eventos');
    controlador
      .expectOne('/api/eventos')
      .flush(
        { code: 'UNAUTHORIZED', message: 'x', timestamp: 't' },
        { status: 401, statusText: 'Unauthorized' },
      );
    expect(router.navigateByUrl).toHaveBeenCalledWith('/login', { replaceUrl: true });
  });
});
