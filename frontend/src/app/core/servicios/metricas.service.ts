import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { construirParams } from '../api/params';
import type { MetricasFiltros, MetricasResponse } from '../api/api-types';

/** Métricas de recuperación de margen para el tablero (Finanzas / Admin). */
@Injectable({ providedIn: 'root' })
export class MetricasService {
  private readonly http = inject(HttpClient);

  recuperacion(filtros: MetricasFiltros = {}): Observable<MetricasResponse> {
    return this.http.get<MetricasResponse>('/api/metricas/recuperacion', {
      params: construirParams(filtros),
    });
  }
}
