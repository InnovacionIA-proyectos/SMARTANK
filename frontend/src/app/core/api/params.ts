import { HttpParams } from '@angular/common/http';

/** Valores aceptados en un query string; `undefined`, `null` y '' se omiten. */
export type ParamValor = string | number | boolean | null | undefined;

/** Construye `HttpParams` descartando los filtros vacíos (equivale al `query` del cliente original). */
export function construirParams(filtros: object = {}): HttpParams {
  let params = new HttpParams();
  for (const [clave, valor] of Object.entries(filtros) as [string, ParamValor][]) {
    if (valor === undefined || valor === null || valor === '') continue;
    params = params.set(clave, String(valor));
  }
  return params;
}
