import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { construirParams } from '../api/params';
import type { EstacionesResponse, SyncPreciosRequest, SyncPreciosResponse } from '../api/api-types';

/** Estaciones de servicio (EDS) y sincronización de precios. */
@Injectable({ providedIn: 'root' })
export class EstacionesService {
  private readonly http = inject(HttpClient);

  listar(ruta?: string | null): Observable<EstacionesResponse> {
    return this.http.get<EstacionesResponse>('/api/estaciones', {
      params: construirParams({ ruta }),
    });
  }

  /** Solo `admin`. Sin cuerpo consulta las fuentes reales; con `precios` carga manual. */
  sincronizarPrecios(cuerpo?: SyncPreciosRequest): Observable<SyncPreciosResponse> {
    return this.http.post<SyncPreciosResponse>('/api/estaciones/precios/sync', cuerpo ?? null);
  }
}
