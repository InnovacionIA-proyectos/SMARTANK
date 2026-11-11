import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { construirParams } from '../api/params';
import type {
  AutonomiaResponse,
  RecomendacionRequest,
  RecomendacionResponse,
  RutaViajeApi,
  ViajesFiltros,
  ViajesResponse,
} from '../api/api-types';

/** Viajes: listado filtrado en servidor, ruta, autonomía y recomendación de tanqueo. */
@Injectable({ providedIn: 'root' })
export class ViajesService {
  private readonly http = inject(HttpClient);

  listar(filtros: ViajesFiltros = {}): Observable<ViajesResponse> {
    return this.http.get<ViajesResponse>('/api/viajes', { params: construirParams(filtros) });
  }

  ruta(viajeId: string): Observable<RutaViajeApi> {
    return this.http.get<RutaViajeApi>(`/api/viajes/${encodeURIComponent(viajeId)}/ruta`);
  }

  calcularAutonomia(viajeId: string): Observable<AutonomiaResponse> {
    return this.http.post<AutonomiaResponse>(
      `/api/viajes/${encodeURIComponent(viajeId)}/autonomia`,
      null,
    );
  }

  /** Genera (o actualiza) la propuesta de tanqueo del viaje. El cuerpo es opcional. */
  recomendar(viajeId: string, cuerpo?: RecomendacionRequest): Observable<RecomendacionResponse> {
    return this.http.post<RecomendacionResponse>(
      `/api/viajes/${encodeURIComponent(viajeId)}/recomendacion`,
      cuerpo ?? null,
    );
  }
}
