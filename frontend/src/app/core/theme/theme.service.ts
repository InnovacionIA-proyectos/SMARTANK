import { DOCUMENT } from '@angular/common';
import { Injectable, effect, inject, signal } from '@angular/core';

export type Tema = 'light' | 'dark';

const CLAVE_TEMA = 'smartank-theme';

/**
 * Tema claro/oscuro. Persiste la preferencia del usuario y aplica la clase `.dark`
 * en <html>; el modo oscuro aplica a todas las pantallas. Sin preferencia guardada
 * se respeta `prefers-color-scheme`.
 */
@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly document = inject(DOCUMENT);

  private readonly _tema = signal<Tema>(this.leerInicial());
  readonly tema = this._tema.asReadonly();

  constructor() {
    effect(() => {
      const tema = this._tema();
      this.document.documentElement.classList.toggle('dark', tema === 'dark');
      try {
        this.document.defaultView?.localStorage.setItem(CLAVE_TEMA, tema);
      } catch {
        // almacenamiento no disponible (modo privado): el tema sigue funcionando en memoria
      }
    });
  }

  alternar(): void {
    this._tema.update((t) => (t === 'dark' ? 'light' : 'dark'));
  }

  fijar(tema: Tema): void {
    this._tema.set(tema);
  }

  private leerInicial(): Tema {
    const ventana = this.document.defaultView;
    try {
      const guardado = ventana?.localStorage.getItem(CLAVE_TEMA);
      if (guardado === 'light' || guardado === 'dark') return guardado;
    } catch {
      // se ignora y se cae a la preferencia del sistema
    }
    return ventana?.matchMedia?.('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
  }
}
