import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/** Dato del viaje resaltado (placa, manifiesto, ruta) con letra grande. */
@Component({
  selector: 'app-dato-viaje',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'block min-w-0' },
  template: `
    <p class="text-caption uppercase tracking-wide text-text-muted">{{ label() }}</p>
    <p class="break-words text-h3 font-semibold leading-tight text-text-primary">{{ valor() }}</p>
  `,
})
export class DatoViaje {
  readonly label = input.required<string>();
  readonly valor = input.required<string>();
}

/** Métrica grande (autonomía, galones, precio techo, ahorro). `acento` la pinta en verde. */
@Component({
  selector: 'app-metrica',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'block min-w-0' },
  template: `
    <p class="text-caption text-text-muted">{{ label() }}</p>
    <p [class]="acento() ? claseAcento : claseNormal">{{ valor() }}</p>
  `,
})
export class Metrica {
  readonly label = input.required<string>();
  readonly valor = input.required<string>();
  readonly acento = input(false);

  protected readonly claseNormal = 'break-words text-h2 leading-tight tabular text-text-primary';
  protected readonly claseAcento = 'break-words text-h2 leading-tight tabular text-accent';
}
