import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, catchError, finalize, map, of, shareReplay, tap } from 'rxjs';
import type { LoginRequest, LoginResponse, MeResponse, Rol, UsuarioSesion } from '../api/api-types';

/**
 * Estado de sesión de la SPA. El token vive en una cookie httpOnly (`smartank_session`)
 * que el navegador envía solo; aquí únicamente se guarda el perfil del usuario, que
 * se recupera con `GET /api/auth/me` al arrancar o recargar la página.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  private readonly _usuario = signal<UsuarioSesion | null>(null);
  /** Indica si ya se intentó restaurar la sesión (evita parpadeos de pantalla de login). */
  private readonly _restaurada = signal(false);
  private restauracion$: Observable<boolean> | null = null;

  readonly usuario = this._usuario.asReadonly();
  readonly restaurada = this._restaurada.asReadonly();
  readonly estaAutenticado = computed(() => this._usuario() !== null);
  readonly rol = computed<Rol | null>(() => this._usuario()?.rol ?? null);

  /** `true` si el usuario autenticado tiene alguno de los roles indicados. */
  tieneRol(roles: readonly Rol[]): boolean {
    const rol = this.rol();
    return rol !== null && roles.includes(rol);
  }

  login(credenciales: LoginRequest): Observable<UsuarioSesion> {
    return this.http.post<LoginResponse>('/api/auth/login', credenciales).pipe(
      map((r) => r.usuario),
      tap((usuario) => {
        this._usuario.set(usuario);
        this._restaurada.set(true);
      }),
    );
  }

  /**
   * Cierra la sesión en el backend (borra la cookie) y vuelve al login. Aunque la
   * llamada falle se limpia el estado local y se navega: el usuario no debe quedar
   * atrapado en una pantalla protegida.
   */
  logout(): Observable<void> {
    return this.http.post<unknown>('/api/auth/logout', null).pipe(
      catchError(() => of(null)),
      map(() => undefined),
      finalize(() => {
        this.limpiarSesion();
        // replace: tras cerrar sesión, "atrás" no debe volver a una pantalla protegida.
        void this.router.navigateByUrl('/login', { replaceUrl: true });
      }),
    );
  }

  /**
   * Restaura la sesión con `GET /api/auth/me`. Devuelve `true` si hay sesión válida.
   * Es idempotente: varias guardas simultáneas comparten una única petición, y una
   * vez restaurada responde de inmediato con el estado en memoria.
   */
  restaurar(): Observable<boolean> {
    if (this._restaurada()) return of(this.estaAutenticado());
    this.restauracion$ ??= this.http.get<MeResponse>('/api/auth/me').pipe(
      tap((usuario) => this._usuario.set(usuario)),
      map(() => true),
      catchError(() => {
        this._usuario.set(null);
        return of(false);
      }),
      tap(() => this._restaurada.set(true)),
      finalize(() => (this.restauracion$ = null)),
      shareReplay({ bufferSize: 1, refCount: false }),
    );
    return this.restauracion$;
  }

  /** Descarta la sesión local (401 del API o logout). No llama al backend. */
  limpiarSesion(): void {
    this._usuario.set(null);
    this._restaurada.set(true);
  }
}
