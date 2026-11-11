import { ChangeDetectionStrategy, Component, effect, signal } from '@angular/core';
import { Icon } from '../shared/ui/icon';
import { SidebarNav } from './sidebar-nav';

const CLAVE_COLAPSADO = 'smartank-sidebar-colapsado';

function leerColapsado(): boolean {
  try {
    return localStorage.getItem(CLAVE_COLAPSADO) === '1';
  } catch {
    return false;
  }
}

/**
 * Barra lateral de navegación colapsable. En estado colapsado muestra solo los
 * iconos (ancho reducido) y en expandido el logo, las etiquetas y el pie.
 * El estado se recuerda en localStorage entre sesiones.
 */
@Component({
  selector: 'app-sidebar',
  imports: [Icon, SidebarNav],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    '[class]': 'clases()',
  },
  template: `
    <div class="mb-6 flex flex-col gap-2">
      @if (!colapsado()) {
        <img src="logo-combinado.png" alt="SMARTANK · Botero Soto" class="w-full rounded-md" />
      }
      <div [class]="colapsado() ? 'flex justify-center' : 'flex justify-end'">
        <button
          type="button"
          (click)="alternar()"
          [attr.aria-label]="colapsado() ? 'Expandir menú' : 'Colapsar menú'"
          [attr.title]="colapsado() ? 'Expandir menú' : 'Colapsar menú'"
          class="rounded-md p-2 text-text-muted transition-colors hover:bg-border/40"
        >
          <app-icon [name]="colapsado() ? 'panel-left-open' : 'panel-left-close'" [size]="20" />
        </button>
      </div>
    </div>

    <app-sidebar-nav [colapsado]="colapsado()" />

    @if (!colapsado()) {
      <div class="mt-auto px-3 pt-4 text-caption text-text-muted">Botero Soto · Piloto</div>
    }
  `,
})
export class Sidebar {
  protected readonly colapsado = signal(leerColapsado());

  protected readonly clases = () =>
    `hidden shrink-0 flex-col border-r border-border bg-surface p-4 transition-[width] duration-200 md:flex ${
      this.colapsado() ? 'w-20' : 'w-64'
    }`;

  constructor() {
    effect(() => {
      try {
        localStorage.setItem(CLAVE_COLAPSADO, this.colapsado() ? '1' : '0');
      } catch {
        // almacenamiento no disponible: la preferencia solo vive en memoria
      }
    });
  }

  protected alternar(): void {
    this.colapsado.update((c) => !c);
  }
}
