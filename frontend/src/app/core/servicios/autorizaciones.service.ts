import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import type {
  AutorizacionCreateRequest,
  AutorizacionDetalleApi,
  AutorizacionPublica,
  NotificarResponse,
  PreautorizarEdsResponse,
} from '../api/api-types';

/** Autorizaciones de tanqueo y sus acciones (autorizar, pre-autorizar en la EDS, notificar). */
@Injectable({ providedIn: 'root' })
export class AutorizacionesService {
  private readonly http = inject(HttpClient);

  detalle(id: string): Observable<AutorizacionDetalleApi> {
    return this.http.get<AutorizacionDetalleApi>(`/api/autorizaciones/${encodeURIComponent(id)}`);
  }

  crear(cuerpo: AutorizacionCreateRequest): Observable<AutorizacionPublica> {
    return this.http.post<AutorizacionPublica>('/api/autorizaciones', cuerpo);
  }

  autorizar(id: string): Observable<AutorizacionPublica> {
    return this.http.post<AutorizacionPublica>(
      `/api/autorizaciones/${encodeURIComponent(id)}/autorizar`,
      null,
    );
  }

  preautorizarEds(id: string): Observable<PreautorizarEdsResponse> {
    return this.http.post<PreautorizarEdsResponse>(
      `/api/autorizaciones/${encodeURIComponent(id)}/preautorizar-eds`,
      null,
    );
  }

  notificar(id: string): Observable<NotificarResponse> {
    return this.http.post<NotificarResponse>(
      `/api/autorizaciones/${encodeURIComponent(id)}/notificar`,
      null,
    );
  }
}
