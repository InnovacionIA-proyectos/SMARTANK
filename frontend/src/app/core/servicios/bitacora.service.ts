import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { construirParams } from '../api/params';
import type { BitacoraOrdenApi, UploadResponse } from '../api/api-types';

/**
 * Bitácora móvil del conductor. Es pública: se identifica con el token firmado que
 * llega en el enlace de WhatsApp, no con la sesión de un usuario.
 */
@Injectable({ providedIn: 'root' })
export class BitacoraService {
  private readonly http = inject(HttpClient);

  /** Datos de la orden asociada al token (401 `BITACORA_TOKEN_INVALIDO` si no es válido). */
  obtener(token: string): Observable<BitacoraOrdenApi> {
    return this.http.get<BitacoraOrdenApi>(`/api/bitacora/${encodeURIComponent(token)}`);
  }

  /** Sube la foto del surtidor como bytes crudos (`Content-Type: image/*`, máx. 5 MB). */
  subirFoto(token: string, imagen: Blob): Observable<UploadResponse> {
    return this.http.post<UploadResponse>('/api/uploads', imagen, {
      params: construirParams({ token }),
      headers: { 'Content-Type': imagen.type || 'image/jpeg' },
    });
  }
}
