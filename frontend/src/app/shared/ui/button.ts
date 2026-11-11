import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

export type VarianteBoton = 'primary' | 'secondary' | 'ghost' | 'success' | 'danger';
export type TamanoBoton = 'sm' | 'md' | 'lg';

const VARIANTES: Record<VarianteBoton, string> = {
  primary: 'bg-primary text-white hover:bg-primary-hover',
  secondary: 'bg-secondary text-white hover:opacity-90',
  ghost: 'bg-transparent text-text-primary hover:bg-border/40',
  success: 'bg-success text-white hover:opacity-90',
  danger: 'bg-danger text-white hover:opacity-90',
};

const TAMANOS: Record<TamanoBoton, string> = {
  sm: 'h-8 px-3 text-caption',
  md: 'h-10 px-4 text-body',
  lg: 'h-12 px-6 text-body',
};

/**
 * Botón del design system. Se aplica sobre un `<button>` (o `<a>`) nativo para
 * conservar su semántica: `<button appButton variant="success" (click)="...">`.
 */
@Component({
  // Selector de atributo a propósito: conserva la semántica nativa de <button> y <a>.
  // eslint-disable-next-line @angular-eslint/component-selector
  selector: 'button[appButton], a[appButton]',
  template: '<ng-content />',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '[class]': 'clases()' },
})
export class Button {
  readonly variant = input<VarianteBoton>('primary');
  readonly size = input<TamanoBoton>('md');

  protected readonly clases = computed(
    () =>
      `inline-flex items-center justify-center gap-2 rounded-md font-medium transition-colors disabled:cursor-not-allowed disabled:opacity-50 ${VARIANTES[this.variant()]} ${TAMANOS[this.size()]}`,
  );
}
