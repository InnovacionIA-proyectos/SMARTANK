import { HttpErrorResponse } from '@angular/common/http';
import type { ErrorApiCuerpo } from './api-types';

/** Mensaje genérico cuando no hay respuesta del servidor (red caída, CORS, timeout). */
export const MENSAJE_SIN_CONEXION = 'Error de conexión con el servidor';

/** Error del API normalizado: lo que las pantallas muestran al usuario es `message`. */
export class ApiError extends Error {
  readonly code: string;
  readonly status: number;
  readonly details?: unknown;
  readonly timestamp?: string;

  constructor(
    message: string,
    code: string,
    status: number,
    details?: unknown,
    timestamp?: string,
  ) {
    super(message);
    this.name = 'ApiError';
    this.code = code;
    this.status = status;
    this.details = details;
    this.timestamp = timestamp;
  }
}

/** Mensajes de respaldo por código HTTP cuando el cuerpo no trae `message`. */
function mensajePorEstado(status: number): string {
  switch (status) {
    case 0:
      return MENSAJE_SIN_CONEXION;
    case 400:
      return 'La solicitud no es válida';
    case 401:
      return 'Tu sesión no es válida o expiró';
    case 403:
      return 'No tienes permiso para realizar esta acción';
    case 404:
      return 'El recurso solicitado no existe';
    case 409:
      return 'La operación no se puede realizar en el estado actual';
    case 429:
      return 'Demasiadas solicitudes, intenta de nuevo en unos segundos';
    case 502:
    case 503:
    case 504:
      return 'El servicio no está disponible, intenta de nuevo más tarde';
    default:
      return status >= 500 ? 'Error interno del servidor' : `Error ${status}`;
  }
}

function esCuerpoError(valor: unknown): valor is Partial<ErrorApiCuerpo> {
  return typeof valor === 'object' && valor !== null;
}

/**
 * Convierte un `HttpErrorResponse` en `ApiError`. El cuerpo esperado es
 * `{ code, message, timestamp, details }`; si el servidor no lo envía (proxy,
 * 502 de nginx, etc.) se usa un mensaje en español según el código HTTP.
 */
export function aApiError(err: HttpErrorResponse): ApiError {
  const cuerpo = esCuerpoError(err.error) ? err.error : null;
  const message =
    cuerpo && typeof cuerpo.message === 'string' && cuerpo.message.trim()
      ? cuerpo.message
      : mensajePorEstado(err.status);
  const code = cuerpo && typeof cuerpo.code === 'string' ? cuerpo.code : 'HTTP_ERROR';
  return new ApiError(message, code, err.status, cuerpo?.details, cuerpo?.timestamp);
}

/** Texto para mostrar al usuario a partir de cualquier error capturado. */
export function mensajeDeError(err: unknown, porDefecto = MENSAJE_SIN_CONEXION): string {
  return err instanceof ApiError ? err.message : porDefecto;
}
