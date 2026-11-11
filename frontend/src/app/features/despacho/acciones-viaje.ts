import { ChangeDetectionStrategy, Component, inject, input, output, signal } from '@angular/core';
import { Router } from '@angular/router';
import { mensajeDeError } from '../../core/api/api-error';
import type { ViajeApi } from '../../core/api/api-types';
import { ViajesService } from '../../core/servicios/viajes.service';
import { Button } from '../../shared/ui/button';

/** Acciones por fila del despacho: actualizar autonomía y recomendar estación (EDS). */
@Component({
  selector: 'app-acciones-viaje',
  imports: [Button],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'flex flex-col items-end gap-1' },
  template: `
    <div class="flex gap-2">
      <button
        appButton
        size="sm"
        variant="secondary"
        type="button"
        (click)="calcularAutonomia()"
        [disabled]="pendiente() !== null"
      >
        {{ pendiente() === 'autonomia' ? '…' : 'Actualizar Autonomía' }}
      </button>
      <button
        appButton
        size="sm"
        type="button"
        (click)="recomendar()"
        [disabled]="pendiente() !== null"
      >
        {{ pendiente() === 'recomendacion' ? '…' : 'Recomendar EDS' }}
      </button>
    </div>
    @if (error()) {
      <span class="text-caption text-danger">{{ error() }}</span>
    }
  `,
})
export class AccionesViaje {
  readonly viaje = input.required<ViajeApi>();
  /** Se emite cuando una acción modificó datos y la tabla debe recargarse. */
  readonly cambio = output<void>();

  private readonly viajes = inject(ViajesService);
  private readonly router = inject(Router);

  protected readonly pendiente = signal<null | 'autonomia' | 'recomendacion'>(null);
  protected readonly error = signal<string | null>(null);

  protected calcularAutonomia(): void {
    this.pendiente.set('autonomia');
    this.error.set(null);
    this.viajes.calcularAutonomia(this.viaje().id).subscribe({
      next: () => {
        this.pendiente.set(null);
        this.cambio.emit();
      },
      error: (e: unknown) => {
        this.error.set(mensajeDeError(e, 'Error'));
        this.pendiente.set(null);
      },
    });
  }

  protected recomendar(): void {
    this.pendiente.set('recomendacion');
    this.error.set(null);
    // Siempre genera la propuesta y abre el panel de Recomendación; allí se
    // decide (según autonomía) si se habilita o no autorizar el tanqueo.
    this.viajes.recomendar(this.viaje().id).subscribe({
      next: (r) => {
        if (r.autorizacion) {
          void this.router.navigate(['/recomendacion'], { queryParams: { id: r.autorizacion.id } });
          return;
        }
        this.error.set('No se pudo generar la recomendación');
        this.pendiente.set(null);
      },
      error: (e: unknown) => {
        this.error.set(mensajeDeError(e, 'Error'));
        this.pendiente.set(null);
      },
    });
  }
}
