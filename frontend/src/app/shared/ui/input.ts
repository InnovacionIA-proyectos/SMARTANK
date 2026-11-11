import { ChangeDetectionStrategy, Component, computed, input, model } from '@angular/core';

let siguienteId = 0;

/**
 * Campo de texto con label visible (accesibilidad: nunca usar placeholder como
 * label), ayuda y error. `inputClass` se aplica al `<input>` (p. ej. el ancho).
 * Uso: `<app-input label="Placa" [(value)]="placa" inputClass="w-40" />`.
 */
@Component({
  selector: 'app-input',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'flex flex-col gap-1' },
  template: `
    @if (label()) {
      <label [for]="inputId()" class="text-caption font-medium text-text-muted">{{
        label()
      }}</label>
    }
    <input
      [id]="inputId()"
      [type]="type()"
      [value]="value()"
      [placeholder]="placeholder()"
      [required]="required()"
      [disabled]="disabled()"
      [attr.autocomplete]="autocomplete() || null"
      [attr.inputmode]="inputmode() || null"
      [attr.step]="step() || null"
      [attr.min]="min() || null"
      [attr.aria-invalid]="error() ? true : null"
      [class]="clases()"
      (input)="value.set($any($event.target).value)"
    />
    @if (error()) {
      <span class="text-caption text-danger">{{ error() }}</span>
    } @else if (helper()) {
      <span class="text-caption text-text-muted">{{ helper() }}</span>
    }
  `,
})
export class Input {
  readonly label = input('');
  readonly helper = input('');
  readonly error = input('');
  readonly type = input('text');
  readonly placeholder = input('');
  readonly required = input(false);
  readonly disabled = input(false);
  readonly autocomplete = input('');
  readonly inputmode = input('');
  readonly step = input('');
  readonly min = input('');
  readonly id = input('');
  readonly inputClass = input('');
  readonly value = model('');

  private readonly idAuto = `app-input-${++siguienteId}`;
  protected readonly inputId = computed(() => this.id() || this.idAuto);

  protected readonly clases = computed(
    () =>
      `h-10 rounded-md border bg-surface px-3 text-body text-text-primary placeholder:text-text-muted/60 focus:border-secondary disabled:cursor-not-allowed disabled:opacity-50 ${this.error() ? 'border-danger' : 'border-border'} ${this.inputClass()}`,
  );
}
