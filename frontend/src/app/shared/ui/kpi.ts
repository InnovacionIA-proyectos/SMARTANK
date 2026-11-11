import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { Card } from './card';

/** Tarjeta de KPI: valor grande (métrica tabular) + label + detalle opcional. */
@Component({
  selector: 'app-kpi',
  imports: [Card],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'block' },
  template: `
    <app-card [class]="destacado() ? 'border-success bg-success/5' : ''">
      <p class="text-caption text-text-muted">{{ label() }}</p>
      <p [class]="claseValor()">{{ valor() }}</p>
      @if (sub()) {
        <p class="mt-1 text-caption text-text-muted">{{ sub() }}</p>
      }
    </app-card>
  `,
})
export class Kpi {
  readonly label = input.required<string>();
  readonly valor = input.required<string>();
  /** Resalta el KPI en verde (p. ej. el ahorro). */
  readonly destacado = input(false);
  /** Color del valor: `danger` o `warning` para semáforos. */
  readonly variant = input<'danger' | 'warning' | undefined>(undefined);
  readonly sub = input('');

  protected readonly claseValor = computed(() => {
    const color = this.destacado()
      ? 'text-success'
      : this.variant() === 'danger'
        ? 'text-danger'
        : this.variant() === 'warning'
          ? 'text-warning'
          : 'text-text-primary';
    return `text-metric tabular ${color}`;
  });
}
