import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { construirParams } from '../api/params';
import type {
  AlertaGestionRequest,
  AlertaPublica,
  AlertasResponse,
  EstadoAlerta,
} from '../api/api-types';

/** Bandeja de alertas (Control de Flota / Admin). */
@Injectable({ providedIn: 'root' })
export class AlertasService {
  private readonly http = inject(HttpClient);

  listar(estado?: string): Observable<AlertasResponse> {
    return this.http.get<AlertasResponse>('/api/alertas', { params: construirParams({ estado }) });
  }

  cambiarEstado(id: string, estado: EstadoAlerta): Observable<AlertaPublica> {
    const cuerpo: AlertaGestionRequest = { estado };
    return this.http.patch<AlertaPublica>(`/api/alertas/${encodeURIComponent(id)}`, cuerpo);
  }
}
