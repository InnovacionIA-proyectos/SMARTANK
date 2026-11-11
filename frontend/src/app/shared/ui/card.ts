import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

/** Tarjeta de contenido. `alert` aplica borde danger para anomalías. */
@Component({
  selector: 'app-card',
  template: '<ng-content />',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '[class]': 'clases()' },
})
export class Card {
  readonly variant = input<'default' | 'alert'>('default');

  protected readonly clases = computed(
    () =>
      `block rounded-lg border bg-surface p-6 ${this.variant() === 'alert' ? 'border-danger' : 'border-border'}`,
  );
}
