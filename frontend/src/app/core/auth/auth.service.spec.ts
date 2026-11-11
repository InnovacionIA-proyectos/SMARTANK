import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import type { UsuarioSesion } from '../api/api-types';
import { AuthService } from './auth.service';

const USUARIO: UsuarioSesion = {
  id: 'u1',
  email: 'ana@smartank.co',
  nombre: 'Ana',
  rol: 'despacho',
};

describe('AuthService', () => {
  let auth: AuthService;
  let controlador: HttpTestingController;
  let router: Router;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    });
    auth = TestBed.inject(AuthService);
    controlador = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
    vi.spyOn(router, 'navigateByUrl').mockResolvedValue(true);
  });

  afterEach(() => controlador.verify());

  it('login envía las credenciales y guarda el usuario de la respuesta', () => {
    let resultado: UsuarioSesion | undefined;
    auth.login({ email: USUARIO.email, password: 'secreto' }).subscribe((u) => (resultado = u));

    const req = controlador.expectOne('/api/auth/login');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ email: USUARIO.email, password: 'secreto' });
    req.flush({ usuario: USUARIO });

    expect(resultado).toEqual(USUARIO);
    expect(auth.usuario()).toEqual(USUARIO);
    expect(auth.estaAutenticado()).toBe(true);
    expect(auth.rol()).toBe('despacho');
  });

  it('restaurar consulta /api/auth/me y deja la sesión en memoria', () => {
    let ok: boolean | undefined;
    auth.restaurar().subscribe((v) => (ok = v));
    controlador.expectOne('/api/auth/me').flush(USUARIO);

    expect(ok).toBe(true);
    expect(auth.usuario()).toEqual(USUARIO);
    expect(auth.restaurada()).toBe(true);
  });

  it('restaurar comparte una sola petición entre llamadas simultáneas y luego responde desde memoria', () => {
    const resultados: boolean[] = [];
    auth.restaurar().subscribe((v) => resultados.push(v));
    auth.restaurar().subscribe((v) => resultados.push(v));
    controlador.expectOne('/api/auth/me').flush(USUARIO);
    expect(resultados).toEqual([true, true]);

    auth.restaurar().subscribe((v) => resultados.push(v));
    controlador.expectNone('/api/auth/me');
    expect(resultados).toEqual([true, true, true]);
  });

  it('restaurar devuelve false cuando no hay sesión (401)', () => {
    let ok: boolean | undefined;
    auth.restaurar().subscribe((v) => (ok = v));
    controlador
      .expectOne('/api/auth/me')
      .flush({ message: 'x' }, { status: 401, statusText: 'Unauthorized' });

    expect(ok).toBe(false);
    expect(auth.estaAutenticado()).toBe(false);
    expect(auth.restaurada()).toBe(true);
  });

  it('logout llama al backend, limpia la sesión y navega a /login reemplazando el historial', () => {
    auth.login({ email: USUARIO.email, password: 'x' }).subscribe();
    controlador.expectOne('/api/auth/login').flush({ usuario: USUARIO });

    auth.logout().subscribe();
    const req = controlador.expectOne('/api/auth/logout');
    expect(req.request.method).toBe('POST');
    req.flush({ ok: true });

    expect(auth.estaAutenticado()).toBe(false);
    expect(router.navigateByUrl).toHaveBeenCalledWith('/login', { replaceUrl: true });
  });

  it('logout limpia y navega aunque el backend falle', () => {
    auth.login({ email: USUARIO.email, password: 'x' }).subscribe();
    controlador.expectOne('/api/auth/login').flush({ usuario: USUARIO });

    auth.logout().subscribe();
    controlador.expectOne('/api/auth/logout').error(new ProgressEvent('error'));

    expect(auth.estaAutenticado()).toBe(false);
    expect(router.navigateByUrl).toHaveBeenCalledWith('/login', { replaceUrl: true });
  });

  it('tieneRol evalúa contra el rol del usuario en sesión', () => {
    expect(auth.tieneRol(['admin'])).toBe(false);
    auth.login({ email: USUARIO.email, password: 'x' }).subscribe();
    controlador.expectOne('/api/auth/login').flush({ usuario: USUARIO });
    expect(auth.tieneRol(['despacho', 'admin'])).toBe(true);
    expect(auth.tieneRol(['finanzas'])).toBe(false);
  });
});
