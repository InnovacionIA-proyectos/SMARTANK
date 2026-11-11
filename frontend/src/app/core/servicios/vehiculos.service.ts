import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import type { VehiculoTelemetriaResponse } from '../api/api-types';

/** Último estado de tanque/odómetro de un vehículo. */
@Injectable({ providedIn: 'root' })
export class VehiculosService {
  private readonly http = inject(HttpClient);

  telemetria(vehiculoId: string): Observable<VehiculoTelemetriaResponse> {
    return this.http.get<VehiculoTelemetriaResponse>(
      `/api/vehiculos/${encodeURIComponent(vehiculoId)}/telemetria`,
    );
  }
}
