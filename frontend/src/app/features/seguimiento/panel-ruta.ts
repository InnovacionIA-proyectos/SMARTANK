import { ChangeDetectionStrategy, Component, inject, input, output } from '@angular/core';
import { ViajesService } from '../../core/servicios/viajes.service';
import { MapaRuta } from '../../shared/mapa/mapa-ruta';
import { Button } from '../../shared/ui/button';
import { Card } from '../../shared/ui/card';
import { Skeleton } from '../../shared/ui/skeleton';
import { EmptyState, ErrorState } from '../../shared/ui/states';
import { cargarApi } from '../../shared/utils/cargar-api';

/** Panel con el mapa de la ruta del viaje + tramo de autonomía. */
@Component({
  selector: 'app-panel-ruta',
  imports: [Card, Button, Skeleton, EmptyState, ErrorState, MapaRuta],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'block' },
  template: `
    <app-card>
      <div class="mb-3 flex items-center justify-between">
        <div>
          <h2 class="text-h2">Ruta y autonomía · {{ placa() }}</h2>
          @if (carga.data()?.viaje?.ruta) {
            <p class="text-caption text-text-muted">{{ carga.data()?.viaje?.ruta }}</p>
          }
        </div>
        <button appButton variant="ghost" type="button" (click)="cerrar.emit()">Cerrar</button>
      </div>

      @if (carga.loading() && !carga.data()) {
        <app-skeleton class="h-[32rem] w-full" />
      } @else if (carga.error()) {
        <app-error-state
          [mensaje]="carga.error() ?? ''"
          [conReintento]="true"
          (reintentar)="carga.refetch()"
        />
      } @else if (carga.data(); as data) {
        @if (!data.ruta) {
          <app-empty-state
            titulo="Sin ruta"
            mensaje="Este viaje no tiene ruta calculada (Valhalla) para dibujar en el mapa."
          />
        } @else {
          <!-- Leaflet se descarga bajo demanda, al abrir el mapa. -->
          @defer (on immediate) {
            <app-mapa-ruta
              [polilinea]="data.ruta.polilinea"
              [vehiculo]="data.vehiculo"
              [autonomiaKm]="data.autonomia_km"
              [ruta]="data.viaje.ruta"
              [kilometros]="data.ruta.kilometros"
            />
          } @placeholder {
            <app-skeleton class="h-[32rem] w-full" />
          }
        }
      }
    </app-card>
  `,
})
export class PanelRuta {
  readonly viajeId = input.required<string>();
  readonly placa = input.required<string>();
  readonly cerrar = output<void>();

  private readonly viajes = inject(ViajesService);
  protected readonly carga = cargarApi(() => this.viajes.ruta(this.viajeId()));
}
