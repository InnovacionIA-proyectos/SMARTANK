import { ChangeDetectionStrategy, Component, inject, input, output, signal } from '@angular/core';
import { mensajeDeError } from '../../core/api/api-error';
import type { AlertaApi, EstadoAlerta } from '../../core/api/api-types';
import { AlertasService } from '../../core/servicios/alertas.service';
import { Button } from '../../shared/ui/button';

/** Acciones de gestión de una alerta: revisar, gestionar o descartar. */
@Component({
  selector: 'app-gestion-alerta',
  imports: [Button],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'block' },
  template: `
    @if (alerta().estado === 'gestionada' || alerta().estado === 'descartada') {
      <span class="text-caption text-text-muted">—</span>
    } @else {
      <div class="flex flex-col items-end gap-1">
        <div class="flex gap-2">
          @if (alerta().estado === 'nueva') {
            <button
              appButton
              size="sm"
              variant="secondary"
              type="button"
              [disabled]="pendiente() !== null"
              (click)="cambiar('en_revision')"
            >
              Revisar
            </button>
          }
          <button
            appButton
            size="sm"
            variant="success"
            type="button"
            [disabled]="pendiente() !== null"
            (click)="cambiar('gestionada')"
          >
            Gestionar
          </button>
          <button
            appButton
            size="sm"
            variant="danger"
            type="button"
            [disabled]="pendiente() !== null"
            (click)="cambiar('descartada')"
          >
            Descartar
          </button>
        </div>
        @if (error()) {
          <span class="text-caption text-danger">{{ error() }}</span>
        }
      </div>
    }
  `,
})
export class GestionAlerta {
  readonly alerta = input.required<AlertaApi>();
  /** Se emite tras cambiar el estado, para recargar la bandeja. */
  readonly cambio = output<void>();

  private readonly alertas = inject(AlertasService);

  protected readonly pendiente = signal<EstadoAlerta | null>(null);
  protected readonly error = signal<string | null>(null);

  protected cambiar(estado: EstadoAlerta): void {
    this.pendiente.set(estado);
    this.error.set(null);
    this.alertas.cambiarEstado(this.alerta().id, estado).subscribe({
      next: () => {
        // Tras el cambio la bandeja se recarga; este componente se desmonta o se
        // reutiliza para otra alerta, así que se libera el estado de "pendiente".
        this.pendiente.set(null);
        this.cambio.emit();
      },
      error: (e: unknown) => {
        this.error.set(mensajeDeError(e, 'Error'));
        this.pendiente.set(null);
      },
    });
  }
}
