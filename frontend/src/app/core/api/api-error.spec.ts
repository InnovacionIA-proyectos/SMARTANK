import { HttpErrorResponse } from '@angular/common/http';
import { ApiError, MENSAJE_SIN_CONEXION, aApiError, mensajeDeError } from './api-error';

const respuesta = (status: number, error: unknown) => new HttpErrorResponse({ status, error });

describe('aApiError', () => {
  it('toma code, message, details y timestamp del cuerpo del API', () => {
    const err = aApiError(
      respuesta(409, {
        code: 'BITACORA_YA_REGISTRADA',
        message: 'La bitácora ya fue registrada',
        timestamp: '2026-06-17T10:00:00Z',
        details: { campo: 'x' },
      }),
    );
    expect(err).toBeInstanceOf(ApiError);
    expect(err.message).toBe('La bitácora ya fue registrada');
    expect(err.code).toBe('BITACORA_YA_REGISTRADA');
    expect(err.status).toBe(409);
    expect(err.details).toEqual({ campo: 'x' });
    expect(err.timestamp).toBe('2026-06-17T10:00:00Z');
  });

  it('sin cuerpo JSON usa un mensaje en español según el estado HTTP', () => {
    expect(aApiError(respuesta(0, null)).message).toBe(MENSAJE_SIN_CONEXION);
    expect(aApiError(respuesta(401, '<html>')).message).toContain('sesión');
    expect(aApiError(respuesta(403, null)).message).toContain('permiso');
    expect(aApiError(respuesta(404, null)).message).toContain('no existe');
    expect(aApiError(respuesta(429, null)).message).toContain('Demasiadas');
    expect(aApiError(respuesta(502, '<html>')).message).toContain('no está disponible');
    expect(aApiError(respuesta(500, null)).message).toBe('Error interno del servidor');
    expect(aApiError(respuesta(418, null)).message).toBe('Error 418');
  });

  it('ignora un message vacío y usa HTTP_ERROR como código por defecto', () => {
    const err = aApiError(respuesta(400, { message: '   ' }));
    expect(err.message).toBe('La solicitud no es válida');
    expect(err.code).toBe('HTTP_ERROR');
  });
});

describe('mensajeDeError', () => {
  it('devuelve el mensaje de un ApiError y el valor por defecto en otro caso', () => {
    expect(mensajeDeError(new ApiError('Mensaje del API', 'X', 400))).toBe('Mensaje del API');
    expect(mensajeDeError(new Error('TypeError interno'))).toBe(MENSAJE_SIN_CONEXION);
    expect(mensajeDeError(null, 'No se pudo')).toBe('No se pudo');
  });
});
