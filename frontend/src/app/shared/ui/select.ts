import { ChangeDetectionStrategy, Component, computed, input, model } from '@angular/core';

let siguienteId = 0;

export interface OpcionSelect {
  value: string;
  label: string;
}

/**
 * Lista desplegable con label visible, ayuda y error. `selectClass` se aplica al
 * `<select>`. Uso: `<app-select label="Estado" [options]="opciones" [(value)]="estado" />`.
 */
@Component({
  selector: 'app-select',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'flex flex-col gap-1' },
  template: `
    @if (label()) {
      <label [for]="selectId()" class="text-caption font-medium text-text-muted">{{
        label()
      }}</label>
    }
    <select
      [id]="selectId()"
      [disabled]="disabled()"
      [attr.aria-invalid]="error() ? true : null"
      [class]="clases()"
      (change)="value.set($any($event.target).value)"
    >
      @if (placeholder()) {
        <option value="" [selected]="value() === ''">{{ placeholder() }}</option>
      }
      @for (o of options(); track o.value) {
        <option [value]="o.value" [selected]="o.value === value()">{{ o.label }}</option>
      }
    </select>
    @if (error()) {
      <span class="text-caption text-danger">{{ error() }}</span>
    } @else if (helper()) {
      <span class="text-caption text-text-muted">{{ helper() }}</span>
    }
  `,
})
export class Select {
  readonly label = input('');
  readonly helper = input('');
  readonly error = input('');
  readonly placeholder = input('');
  readonly options = input<readonly OpcionSelect[]>([]);
  readonly disabled = input(false);
  readonly id = input('');
  readonly selectClass = input('');
  readonly value = model('');

  private readonly idAuto = `app-select-${++siguienteId}`;
  protected readonly selectId = computed(() => this.id() || this.idAuto);

  protected readonly clases = computed(
    () =>
      `h-10 rounded-md border bg-surface px-3 text-body text-text-primary focus:border-secondary disabled:cursor-not-allowed disabled:opacity-50 ${this.error() ? 'border-danger' : 'border-border'} ${this.selectClass()}`,
  );
}
