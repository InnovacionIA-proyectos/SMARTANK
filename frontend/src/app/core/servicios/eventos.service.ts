import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { construirParams } from '../api/params';
import type {
  EventoCreateRequest,
  EventoPublico,
  EventosFiltros,
  EventosResponse,
} from '../api/api-types';

/** Historial de tanqueos (con sesión) y registro de la bitácora del conductor (con token). */
@Injectable({ providedIn: 'root' })
export class EventosService {
  private readonly http = inject(HttpClient);

  listar(filtros: EventosFiltros = {}): Observable<EventosResponse> {
    return this.http.get<EventosResponse>('/api/eventos', { params: construirParams(filtros) });
  }

  /** Cierra la bitácora: un solo evento por autorización (409 si ya existe). */
  registrar(cuerpo: EventoCreateRequest): Observable<EventoPublico> {
    return this.http.post<EventoPublico>('/api/eventos', cuerpo);
  }
}
