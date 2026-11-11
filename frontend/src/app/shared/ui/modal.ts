import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  effect,
  input,
  output,
  viewChild,
} from '@angular/core';
import { Icon } from './icon';

/**
 * Modal accesible: rol dialog, cierra con Esc o clic en el backdrop y enfoca el
 * diálogo al abrir. El pie se proyecta con `<div footer>...</div>`.
 */
@Component({
  selector: 'app-modal',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '(document:keydown.escape)': 'alEscape()' },
  template: `
    @if (open()) {
      <!-- El cierre por teclado existe (Esc en el documento y botón de cierre); el clic en el fondo es solo un atajo de ratón. -->
      <div
        class="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4"
        (click)="alFondo($event)"
        role="presentation"
      >
        <div
          #dialogo
          role="dialog"
          aria-modal="true"
          [attr.aria-label]="title() || null"
          tabindex="-1"
          class="w-full max-w-lg rounded-lg border border-border bg-surface shadow-lg outline-none"
        >
          <div class="flex items-center justify-between border-b border-border px-6 py-4">
            @if (title()) {
              <h3 class="text-h3 text-text-primary">{{ title() }}</h3>
            }
            <button
              type="button"
              (click)="closed.emit()"
              aria-label="Cerrar"
              class="rounded-md p-1 text-text-muted hover:bg-border/40"
            >
              <app-icon name="x" [size]="20" />
            </button>
          </div>
          <div class="px-6 py-4"><ng-content /></div>
          <div class="flex justify-end gap-2 border-t border-border px-6 py-4 empty:hidden">
            <ng-content select="[footer]" />
          </div>
        </div>
      </div>
    }
  `,
})
export class Modal {
  readonly open = input(false);
  readonly title = input('');
  readonly closed = output<void>();

  private readonly dialogo = viewChild<ElementRef<HTMLElement>>('dialogo');

  constructor() {
    // Foco inicial en el diálogo cuando se abre.
    effect(() => {
      this.dialogo()?.nativeElement.focus();
    });
  }

  /** Solo cierra si el clic cayó en el fondo, no dentro del diálogo. */
  protected alFondo(evento: MouseEvent): void {
    if (evento.target === evento.currentTarget) this.closed.emit();
  }

  protected alEscape(): void {
    if (this.open()) this.closed.emit();
  }
}
