import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { Icon } from './icon';

/** Indicador de carga. Respeta prefers-reduced-motion (anim. neutralizada globalmente). */
@Component({
  selector: 'app-spinner',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  // Giro más lento (3s en vez del 1s por defecto) para una animación más suave.
  template: `<app-icon
    name="loader-2"
    [size]="size()"
    class="animate-spin text-text-muted [animation-duration:3s]"
  />`,
  host: { class: 'inline-flex', role: 'status', 'aria-label': 'Cargando' },
})
export class Spinner {
  readonly size = input(20);
}
