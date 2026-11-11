import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { map } from 'rxjs';
import type { Rol } from '../api/api-types';
import { AuthService } from './auth.service';

/** Ruta de aterrizaje tras iniciar sesión y destino de las redirecciones por falta de permiso. */
export const RUTA_INICIAL = '/despacho';

/** Exige sesión válida: restaura con `/api/auth/me` y, si no hay sesión, envía a `/login`. */
export const authGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  return auth.restaurar().pipe(map((ok) => ok || router.createUrlTree(['/login'])));
};

/**
 * Exige que el rol del usuario esté entre los permitidos. Se encadena después de
 * `authGuard` (que ya restauró la sesión); sin permiso vuelve a la pantalla inicial.
 */
export const rolGuard =
  (...roles: Rol[]): CanActivateFn =>
  () => {
    const auth = inject(AuthService);
    const router = inject(Router);
    return auth
      .restaurar()
      .pipe(map((ok) => (ok && auth.tieneRol(roles)) || router.createUrlTree([RUTA_INICIAL])));
  };
