import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { Button } from './button';
import { Icon } from './icon';
import { Spinner } from './spinner';

/** Estado de carga centrado. */
@Component({
  selector: 'app-loading-state',
  imports: [Spinner],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'flex flex-col items-center justify-center gap-3 py-12 text-text-muted' },
  template: `
    <app-spinner [size]="28" />
    <p class="text-body">{{ mensaje() }}</p>
  `,
})
export class LoadingState {
  readonly mensaje = input('Cargando…');
}

/** Estado de error. No depende solo del color: ícono + texto. */
@Component({
  selector: 'app-error-state',
  imports: [Button, Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'flex flex-col items-center justify-center gap-3 py-12 text-center',
    role: 'alert',
  },
  template: `
    <app-icon name="alert-triangle" [size]="28" class="text-danger" />
    <div>
      <p class="text-body-strong text-text-primary">{{ titulo() }}</p>
      @if (mensaje()) {
        <p class="mt-1 text-body text-text-muted">{{ mensaje() }}</p>
      }
    </div>
    @if (conReintento()) {
      <button appButton variant="secondary" size="sm" type="button" (click)="reintentar.emit()">
        Reintentar
      </button>
    }
  `,
})
export class ErrorState {
  readonly titulo = input('Algo salió mal');
  readonly mensaje = input('');
  /** Muestra el botón "Reintentar" (equivale a pasar `onReintentar` en el original). */
  readonly conReintento = input(false);
  readonly reintentar = output<void>();
}

/** Estado vacío (sin datos que mostrar). La acción opcional se proyecta como contenido. */
@Component({
  selector: 'app-empty-state',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'flex flex-col items-center justify-center gap-3 py-12 text-center' },
  template: `
    <app-icon name="inbox" [size]="28" class="text-text-muted" />
    <div>
      <p class="text-body-strong text-text-primary">{{ titulo() }}</p>
      @if (mensaje()) {
        <p class="mt-1 text-body text-text-muted">{{ mensaje() }}</p>
      }
    </div>
    <ng-content />
  `,
})
export class EmptyState {
  readonly titulo = input('Sin datos');
  readonly mensaje = input('');
}
