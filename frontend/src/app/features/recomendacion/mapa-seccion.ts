import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import type { AutorizacionDetalleApi } from '../../core/api/api-types';
import { EstacionesService } from '../../core/servicios/estaciones.service';
import { ViajesService } from '../../core/servicios/viajes.service';
import { MapaRecomendacion } from '../../shared/mapa/mapa-recomendacion';
import type { VehiculoMapa } from '../../shared/mapa/mapa-ruta';
import { Badge } from '../../shared/ui/badge';
import { Card } from '../../shared/ui/card';
import { Icon } from '../../shared/ui/icon';
import { Skeleton } from '../../shared/ui/skeleton';
import { EmptyState, ErrorState } from '../../shared/ui/states';
import { cargarApi } from '../../shared/utils/cargar-api';

interface Distancia {
  km: number;
  porCarretera: boolean;
}

/** Tarjeta del mapa de la recomendación: ruta real, EDS elegida y alcance de la autonomía. */
@Component({
  selector: 'app-mapa-seccion',
  imports: [Card, Badge, Icon, Skeleton, EmptyState, ErrorState, MapaRecomendacion],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'block' },
  template: `
    <app-card class="space-y-3">
      <div class="flex flex-wrap items-center justify-between gap-2">
        <h3 class="flex items-center gap-2 text-h3">
          <app-icon name="map-pin" [size]="18" class="text-primary" /> Mapa de la ruta
        </h3>
        @if (!requiereTanqueo()) {
          <!-- El combustible estimado cubre la ruta: no se muestra ninguna EDS. -->
          <app-badge variant="success">La autonomía cubre la ruta · no requiere tanqueo</app-badge>
        } @else if (!vehiculo()) {
          <span class="text-caption text-text-muted">Sin ubicación del vehículo</span>
        } @else if (dist(); as d) {
          <div class="flex flex-wrap items-center gap-2">
            <app-badge variant="info">{{ d.km.toFixed(1) }} km a la EDS sobre la ruta</app-badge>
            <!-- Autonomía real: ¿el rango del vehículo cubre la distancia por carretera? -->
            @if (detalle().autonomia_km != null) {
              <app-badge [variant]="d.km <= detalle().autonomia_km! ? 'success' : 'danger'">
                {{
                  d.km <= detalle().autonomia_km!
                    ? 'Alcanza · autonomía ' + detalle().autonomia_km!.toFixed(0) + ' km'
                    : 'No alcanza · faltan ' + (d.km - detalle().autonomia_km!).toFixed(0) + ' km'
                }}
              </app-badge>
            }
          </div>
        } @else {
          <span class="text-caption text-text-muted">Calculando ruta…</span>
        }
      </div>

      @if (estaciones.loading()) {
        <app-skeleton class="h-[32rem] w-full" />
      } @else if (estaciones.error()) {
        <app-error-state [mensaje]="estaciones.error() ?? ''" />
      } @else if (!estaciones.data() || estaciones.data()!.estaciones.length === 0) {
        <app-empty-state titulo="Sin estaciones en la ruta" />
      } @else {
        <!-- Leaflet se descarga bajo demanda, al mostrar el mapa. -->
        @defer (on immediate) {
          <app-mapa-recomendacion
            [estaciones]="requiereTanqueo() ? estaciones.data()!.estaciones : []"
            [estacionElegidaId]="detalle().estacion_id"
            [vehiculo]="vehiculo()"
            [ruta]="nombreRuta()"
            [polilinea]="polilinea()"
            [autonomiaKm]="detalle().autonomia_km"
            (distancia)="alDistancia($event)"
            (estacionesCerca)="nCerca.set($event)"
          />
        } @placeholder {
          <app-skeleton class="h-[32rem] w-full" />
        }
        <div class="flex flex-wrap items-center gap-4 text-caption text-text-muted">
          <span class="flex items-center gap-1.5">
            <span
              class="inline-block h-3 w-3 rounded-full"
              style="background-color: #069e49"
            ></span>
            Ruta tractomula
          </span>
          <span class="flex items-center gap-1.5">
            <span
              class="inline-block h-3 w-3 rounded-full"
              style="background-color: #c62828"
            ></span>
            Autonomía
          </span>
          @if (requiereTanqueo()) {
            <span class="flex items-center gap-1.5">
              <span
                class="inline-block h-3 w-3 rounded-full"
                style="background-color: #0154ba"
              ></span>
              EDS elegida
            </span>
            <span class="flex items-center gap-1.5">
              <span
                class="inline-block h-3 w-3 rounded-full"
                style="background-color: #b45309"
              ></span>
              Otras estaciones
            </span>
          }
          <span class="flex items-center gap-1.5">
            <span
              class="inline-block h-3 w-3 rounded-full"
              style="background-color: #0f1b2d"
            ></span>
            Vehículo
          </span>
          @if (requiereTanqueo()) {
            <span class="ml-auto">
              {{
                nCerca() != null
                  ? nCerca() + ' estación(es) a ≤ 10 km de la EDS elegida'
                  : 'Filtrando estaciones a ≤ 10 km de la EDS elegida'
              }}
            </span>
          }
        </div>
      }
    </app-card>
  `,
})
export class MapaSeccion {
  readonly detalle = input.required<AutorizacionDetalleApi>();
  readonly requiereTanqueo = input.required<boolean>();

  private readonly estacionesSvc = inject(EstacionesService);
  private readonly viajes = inject(ViajesService);

  // Los `computed` de valores primitivos evitan recargar estaciones y ruta cada vez
  // que el detalle se refresca (p. ej. tras autorizar): el mapa no debe desmontarse.
  private readonly estacionRuta = computed(() => this.detalle().estacion_ruta);
  private readonly viajeId = computed(() => this.detalle().viaje_id);

  protected readonly estaciones = cargarApi(() => this.estacionesSvc.listar(this.estacionRuta()));

  // Polilínea REAL de la ruta del viaje (tabla rutas: Valhalla o línea recta DANE).
  private readonly rutaCarga = cargarApi(() => this.viajes.ruta(this.viajeId()));
  protected readonly polilinea = computed(() => this.rutaCarga.data()?.ruta?.polilinea ?? null);

  protected readonly nombreRuta = computed(() => {
    const d = this.detalle();
    return d.viaje_nombre_ruta || d.viaje_codigo_ruta || d.viaje_ruta;
  });

  // Referencia estable: solo cambia cuando cambia la telemetría del vehículo. Si se
  // recreara en cada refresco del detalle, el mapa se redibujaría sin necesidad.
  protected readonly vehiculo = computed<VehiculoMapa | null>(
    () => {
      const d = this.detalle();
      return d.vehiculo_lat != null && d.vehiculo_lng != null
        ? {
            placa: d.placa,
            lat: d.vehiculo_lat,
            lng: d.vehiculo_lng,
            ubicacion_at: d.vehiculo_ubicacion_at,
          }
        : null;
    },
    {
      equal: (a, b) =>
        a === b ||
        (a != null &&
          b != null &&
          a.placa === b.placa &&
          a.lat === b.lat &&
          a.lng === b.lng &&
          a.ubicacion_at === b.ubicacion_at),
    },
  );

  protected readonly dist = signal<Distancia | null>(null);
  protected readonly nCerca = signal<number | null>(null);

  /** Evita re-render si la distancia no cambió (corta cualquier bucle de actualizaciones). */
  protected alDistancia(d: Distancia): void {
    this.dist.update((prev) =>
      prev && Math.abs(prev.km - d.km) < 0.01 && prev.porCarretera === d.porCarretera ? prev : d,
    );
  }
}
