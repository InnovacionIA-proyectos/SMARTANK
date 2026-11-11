import { HttpErrorResponse, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { aApiError } from '../api/api-error';
import { AuthService } from '../auth/auth.service';

/** Endpoints de autenticación: un 401 aquí es un resultado esperado, no una sesión caída. */
const RUTAS_AUTH = ['/api/auth/login', '/api/auth/me', '/api/auth/logout'];

const esApi = (url: string) => url.startsWith('/api/') || url === '/health';

/**
 * Peticiones anónimas de la bitácora móvil del conductor: se autentican con el token
 * firmado del enlace, no con la sesión. Si el token venció, el 401 debe mostrarse en
 * esa pantalla y nunca enviar al conductor a un login que no existe para él.
 */
function esPeticionConToken(req: HttpRequest<unknown>): boolean {
  if (req.url.startsWith('/api/bitacora/')) return true;
  if (req.url.startsWith('/api/uploads') && req.urlWithParams.includes('token=')) return true;
  return req.method === 'POST' && req.url === '/api/eventos';
}

/**
 * Interceptor único del API:
 *  - envía siempre las credenciales (cookie httpOnly de sesión) en las rutas `/api/...`;
 *  - normaliza los errores `{ code, message, timestamp, details }` a `ApiError`;
 *  - ante un 401 de una ruta con sesión limpia el estado y navega a `/login`.
 */
export const apiInterceptor: HttpInterceptorFn = (req, next) => {
  if (!esApi(req.url)) return next(req);

  const auth = inject(AuthService);
  const router = inject(Router);
  const peticion = req.clone({ withCredentials: true });

  return next(peticion).pipe(
    catchError((err: unknown) => {
      if (!(err instanceof HttpErrorResponse)) return throwError(() => err);

      const esRutaAuth = RUTAS_AUTH.some((r) => req.url.startsWith(r));
      if (err.status === 401 && !esRutaAuth && !esPeticionConToken(req)) {
        auth.limpiarSesion();
        void router.navigateByUrl('/login', { replaceUrl: true });
      }
      return throwError(() => aApiError(err));
    }),
  );
};
