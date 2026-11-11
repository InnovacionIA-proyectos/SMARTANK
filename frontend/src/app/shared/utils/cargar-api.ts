import { Signal, effect, signal, untracked } from '@angular/core';
import { Observable } from 'rxjs';
import { MENSAJE_SIN_CONEXION, mensajeDeError } from '../../core/api/api-error';

/** Estado reactivo de una carga de datos: `{ data, loading, error, refetch }`. */
export interface CargaApi<T> {
  /** Último dato recibido; se conserva mientras se recarga para no desmontar la vista. */
  readonly data: Signal<T | null>;
  readonly loading: Signal<boolean>;
  readonly error: Signal<string | null>;
  /** Vuelve a ejecutar la consulta con los mismos parámetros. */
  refetch(): void;
}

/**
 * Carga de datos reactiva (equivalente al hook `useApi` del frontend original).
 *
 * `origen` se evalúa dentro de un `effect`: las señales que lea (filtros, id, ...)
 * son sus dependencias y cualquier cambio cancela la consulta en curso y lanza otra.
 * Si devuelve `null` no se consulta (p. ej. falta el parámetro de la URL).
 * Debe llamarse en contexto de inyección (constructor o inicializador de campo).
 */
export function cargarApi<T>(origen: () => Observable<T> | null): CargaApi<T> {
  const data = signal<T | null>(null);
  const loading = signal(true);
  const error = signal<string | null>(null);
  const nonce = signal(0);

  effect((alLimpiar) => {
    nonce();
    const consulta = origen();
    if (!consulta) {
      loading.set(false);
      return;
    }
    loading.set(true);
    error.set(null);
    const suscripcion = untracked(() =>
      consulta.subscribe({
        next: (valor) => data.set(valor),
        error: (e: unknown) => {
          error.set(mensajeDeError(e, MENSAJE_SIN_CONEXION));
          loading.set(false);
        },
        complete: () => loading.set(false),
      }),
    );
    alLimpiar(() => suscripcion.unsubscribe());
  });

  return {
    data: data.asReadonly(),
    loading: loading.asReadonly(),
    error: error.asReadonly(),
    refetch: () => nonce.update((n) => n + 1),
  };
}
