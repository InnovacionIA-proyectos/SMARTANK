import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

export type VarianteBadge = 'neutral' | 'info' | 'success' | 'warning' | 'danger';

const VARIANTES: Record<VarianteBadge, string> = {
  neutral: 'bg-border/50 text-text-muted',
  info: 'bg-info/15 text-info',
  success: 'bg-success/15 text-success',
  warning: 'bg-warning/15 text-warning',
  danger: 'bg-danger/15 text-danger',
};

/** Badge de estado. No depende solo del color: combinar con ícono/texto. */
@Component({
  selector: 'app-badge',
  template: '<ng-content />',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '[class]': 'clases()' },
})
export class Badge {
  readonly variant = input<VarianteBadge>('neutral');

  protected readonly clases = computed(
    () =>
      `inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-caption font-medium ${VARIANTES[this.variant()]}`,
  );
}
