import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import {
  ActivatedRouteSnapshot,
  CanActivateFn,
  Router,
  RouterStateSnapshot,
  UrlTree,
  provideRouter,
} from '@angular/router';
import { Observable, firstValueFrom, isObservable } from 'rxjs';
import type { Rol, UsuarioSesion } from '../api/api-types';
import { AuthService } from './auth.service';
import { authGuard, rolGuard } from './guards';
import { ROLES_ALERTAS, ROLES_TABLERO } from './roles';

const ruta = {} as ActivatedRouteSnapshot;
const estado = { url: '/x' } as RouterStateSnapshot;

function usuario(rol: Rol): UsuarioSesion {
  return { id: 'u1', email: 'u@smartank.co', nombre: null, rol };
}

describe('guards', () => {
  let controlador: HttpTestingController;
  let router: Router;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    });
    controlador = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
  });

  afterEach(() => controlador.verify());

  /** Ejecuta la guarda y resuelve su resultado (puede ser síncrono u Observable). */
  async function ejecutar(guarda: CanActivateFn, respuestaMe?: () => void) {
    const salida = TestBed.runInInjectionContext(() => guarda(ruta, estado));
    const promesa = isObservable(salida)
      ? firstValueFrom(salida as Observable<boolean | UrlTree>)
      : Promise.resolve(salida);
    respuestaMe?.();
    return promesa;
  }

  const sinSesion = () =>
    controlador
      .expectOne('/api/auth/me')
      .flush({ message: 'x' }, { status: 401, statusText: 'Unauthorized' });

  it('authGuard deja pasar con sesión válida (restaurada vía /api/auth/me)', async () => {
    const resultado = await ejecutar(authGuard, () =>
      controlador.expectOne('/api/auth/me').flush(usuario('despacho')),
    );
    expect(resultado).toBe(true);
  });

  it('authGuard redirige a /login si no hay sesión', async () => {
    const resultado = await ejecutar(authGuard, sinSesion);
    expect(resultado).toBeInstanceOf(UrlTree);
    expect(router.serializeUrl(resultado as UrlTree)).toBe('/login');
  });

  it('authGuard no repite /api/auth/me cuando la sesión ya está restaurada', async () => {
    TestBed.inject(AuthService).limpiarSesion();
    const resultado = await ejecutar(authGuard);
    controlador.expectNone('/api/auth/me');
    expect(resultado).toBeInstanceOf(UrlTree);
  });

  const casos: [Rol, readonly Rol[], boolean][] = [
    ['control_flota', ROLES_ALERTAS, true],
    ['admin', ROLES_ALERTAS, true],
    ['despacho', ROLES_ALERTAS, false],
    ['finanzas', ROLES_ALERTAS, false],
    ['finanzas', ROLES_TABLERO, true],
    ['admin', ROLES_TABLERO, true],
    ['control_flota', ROLES_TABLERO, false],
  ];

  it.each(casos)('rolGuard: %s frente a %j => %s', async (rol, permitidos, esperado) => {
    const resultado = await ejecutar(rolGuard(...permitidos), () =>
      controlador.expectOne('/api/auth/me').flush(usuario(rol)),
    );
    if (esperado) {
      expect(resultado).toBe(true);
    } else {
      expect(resultado).toBeInstanceOf(UrlTree);
      expect(router.serializeUrl(resultado as UrlTree)).toBe('/despacho');
    }
  });

  it('rolGuard no deja pasar sin sesión', async () => {
    const resultado = await ejecutar(rolGuard('admin'), sinSesion);
    expect(resultado).toBeInstanceOf(UrlTree);
  });
});
