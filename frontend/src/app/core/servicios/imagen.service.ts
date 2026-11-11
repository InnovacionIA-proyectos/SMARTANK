import { Injectable } from '@angular/core';
import { comprimirImagen } from '../../shared/utils/imagen';

/**
 * Compresión de fotos del dispositivo antes de subirlas. Envuelve la utilidad pura
 * para poder sustituirla en pruebas (jsdom no decodifica imágenes ni usa canvas).
 */
@Injectable({ providedIn: 'root' })
export class ImagenService {
  comprimir(file: File): Promise<Blob> {
    return comprimirImagen(file);
  }
}
