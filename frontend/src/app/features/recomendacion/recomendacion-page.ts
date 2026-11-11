import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Observable, map } from 'rxjs';
import { ApiError, mensajeDeError } from '../../core/api/api-error';
import type { EstadoAutorizacion } from '../../core/api/api-types';
import { AutorizacionesService } from '../../core/servicios/autorizaciones.service';
import { Badge } from '../../shared/ui/badge';
import { Button } from '../../shared/ui/button';
import { Card } from '../../shared/ui/card';
import { Icon } from '../../shared/ui/icon';
import { EmptyState, ErrorState, LoadingState } from '../../shared/ui/states';
import { cargarApi } from '../../shared/utils/cargar-api';
import { formatFecha } from '../../shared/utils/fecha';
import { copExacto, entero } from '../../shared/utils/formato';
import { progresoRuta } from '../../shared/utils/geo';
import { DatoViaje, Metrica } from './datos';
import { MapaSeccion } from './mapa-seccion';

const ESTADO_BADGE: Record<
  EstadoAutorizacion,
  'neutral' | 'info' | 'success' | 'warning' | 'danger'
> = {
  propuesta: 'info',
  autorizada: 'success',
  ejecutada: 'success',
  ejecutada_parcial: 'warning',
  expirada: 'neutral',
  rechazada: 'danger',
};

type Accion = 'autorizar' | 'eds' | 'notificar';

/**
 * Recomendación de Tanqueo: detalle de la propuesta (estación, galones, precio techo)
 * con una acción dominante (Autorizar) y el mapa de la ruta. El `id` de la autorización
 * llega por query param (`/recomendacion?id=...`).
 */
@Component({
  selector: 'app-recomendacion-page',
  imports: [
    RouterLink,
    Card,
    Badge,
    Button,
    Icon,
    DatoViaje,
    Metrica,
    MapaSeccion,
    LoadingState,
    ErrorState,
    EmptyState,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'block space-y-4' },
  template: `
    <a
      routerLink="/despacho"
      class="inline-flex items-center gap-1 text-caption text-text-muted hover:text-text-primary"
    >
      <app-icon name="arrow-left" [size]="16" /> Volver al Panel de Despacho
    </a>
    <div>
      <h1 class="text-h1">Recomendación de Tanqueo</h1>
      <p class="text-body text-text-muted">
        El sistema ya decidió estación, volumen y precio. Confirma con una sola acción.
      </p>
    </div>

    @if (!id()) {
      <app-empty-state
        titulo="Sin recomendación activa"
        mensaje="Solicita una recomendación desde el Panel de Despacho para ver la estación elegida."
      />
    } @else if (carga.loading() && !carga.data()) {
      <!-- Solo en la carga INICIAL se muestra el loader/error de pantalla completa. En
           un refetch (tras autorizar / pre-autorizar / notificar) ya hay datos, así que
           el detalle y el mapa NO se desmontan (evita el "parpadeo" tipo recarga). -->
      <app-loading-state mensaje="Cargando recomendación…" />
    } @else if (carga.error() && !carga.data()) {
      <app-error-state
        [mensaje]="carga.error() ?? ''"
        [conReintento]="true"
        (reintentar)="carga.refetch()"
      />
    } @else if (carga.data(); as a) {
      @if (vista(); as v) {
        <div class="space-y-4">
          <app-card class="space-y-5">
            <div class="flex items-start justify-between gap-4">
              <h2 class="min-w-0 break-words text-h2">{{ a.estacion_nombre }}</h2>
              <div class="flex shrink-0 items-center gap-2">
                @if (carga.loading()) {
                  <span class="text-caption text-text-muted">Actualizando…</span>
                }
                <app-badge [variant]="estadoBadge[a.estado]">{{ a.estado }}</app-badge>
              </div>
            </div>

            <!-- Datos del viaje — resaltados -->
            <div
              class="grid grid-cols-1 gap-4 border-y border-border py-4 sm:grid-cols-2 lg:grid-cols-4"
            >
              <app-dato-viaje label="Placa" [valor]="a.placa" />
              <app-dato-viaje label="Manifiesto" [valor]="a.manifiesto ?? '—'" />
              <app-dato-viaje
                label="Conductor"
                [valor]="a.conductor_nombre || a.cedula_conductor || '—'"
              />
              <app-dato-viaje label="Programa" [valor]="a.programa ?? '—'" />
              <app-dato-viaje label="Capacidad" [valor]="v.capacidad" />
              <app-dato-viaje label="Nivel tanque" [valor]="v.nivel" />
              <app-dato-viaje label="Última consulta nivel tanque" [valor]="v.fechaTele" />
              <app-dato-viaje label="Galones estimados" [valor]="v.galonesEst" />
              <app-dato-viaje
                class="lg:col-span-2"
                label="Ruta"
                [valor]="a.viaje_nombre_ruta || a.viaje_codigo_ruta || a.viaje_ruta || '—'"
              />
              <app-dato-viaje label="Km ruta" [valor]="v.kmRuta" />
              <app-dato-viaje label="% recorrido" [valor]="v.recorrido" />
            </div>

            <div class="grid grid-cols-2 gap-4 lg:grid-cols-4">
              <app-metrica label="Autonomía (km)" [valor]="v.autonomia" />
              <app-metrica label="Galones autorizados" [valor]="'' + a.galones_max" />
              <app-metrica label="Precio techo" [valor]="cop(a.precio_techo)" />
              <app-metrica
                label="Ahorro estimado"
                [valor]="a.delta_ahorro_estimado != null ? cop(a.delta_ahorro_estimado) : '—'"
                [acento]="true"
              />
            </div>

            <!-- Control del panel: si la autonomía cubre la ruta, no se requiere tanqueo. -->
            @if (a.estado === 'propuesta' && !v.requiereTanqueo) {
              <p class="rounded-md bg-info/10 px-3 py-2 text-caption text-info" role="status">
                La autonomía ({{
                  a.autonomia_km != null ? Math.round(a.autonomia_km) + ' km' : '—'
                }}) cubre la ruta restante{{
                  v.rutaRestante != null ? ' (' + Math.round(v.rutaRestante) + ' km)' : ''
                }}: no requiere tanqueo. Puedes autorizarlo de todos modos si lo consideras
                necesario.
              </p>
            }

            <div class="flex flex-wrap items-center gap-2">
              @if (a.estado === 'propuesta') {
                <button
                  appButton
                  variant="success"
                  size="lg"
                  type="button"
                  [disabled]="accion() !== null || !v.requiereTanqueo"
                  [attr.title]="
                    !v.requiereTanqueo ? 'La autonomía cubre la ruta: no requiere tanqueo' : null
                  "
                  (click)="autorizar(a.id)"
                >
                  {{ accion() === 'autorizar' ? 'Autorizando…' : 'Autorizar' }}
                </button>
              }
              @if (a.estado === 'autorizada') {
                <button
                  appButton
                  variant="secondary"
                  type="button"
                  [disabled]="accion() !== null"
                  (click)="preautorizarEds(a.id)"
                >
                  {{ accion() === 'eds' ? 'Registrando…' : 'Pre-autorizar EDS' }}
                </button>
                <button
                  appButton
                  type="button"
                  [disabled]="accion() !== null"
                  (click)="notificar(a.id)"
                >
                  {{ accion() === 'notificar' ? 'Notificando…' : 'Notificar conductor' }}
                </button>
              }

              <!-- Mensaje de resultado: en la MISMA fila, justo a la derecha de los
                   botones (sin ml-auto, para no empujarlo al borde), y así no desplazar
                   el contenido hacia abajo cuando aparece. -->
              @if (msg(); as m) {
                <p
                  role="alert"
                  [class]="
                    'rounded-md px-3 py-2 text-caption ' +
                    (m.tipo === 'ok' ? 'bg-success/10 text-success' : 'bg-danger/10 text-danger')
                  "
                >
                  {{ m.texto }}
                </p>
              }
            </div>
          </app-card>

          <app-mapa-seccion [detalle]="a" [requiereTanqueo]="v.requiereTanqueo" />
        </div>
      }
    } @else {
      <app-empty-state titulo="Sin datos" />
    }
  `,
})
export class RecomendacionPage {
  /** Id de la autorización (query param `id`). */
  readonly id = input<string | undefined>(undefined);

  private readonly autorizaciones = inject(AutorizacionesService);

  protected readonly carga = cargarApi(() => {
    const id = this.id();
    return id ? this.autorizaciones.detalle(id) : null;
  });

  protected readonly accion = signal<Accion | null>(null);
  protected readonly msg = signal<{ tipo: 'ok' | 'error'; texto: string } | null>(null);

  protected readonly estadoBadge = ESTADO_BADGE;
  protected readonly cop = copExacto;
  protected readonly Math = Math;

  /** Valores derivados del detalle: porcentajes, ruta restante y si REQUIERE tanqueo. */
  protected readonly vista = computed(() => {
    const a = this.carga.data();
    if (!a) return null;

    const pctNivel =
      a.nivel_tanque_actual != null && a.capacidad_tanque_gal
        ? Math.round((a.nivel_tanque_actual / a.capacidad_tanque_gal) * 100)
        : null;
    const galonesEst =
      a.nivel_tanque_actual != null && a.capacidad_tanque_gal
        ? Math.round(a.nivel_tanque_actual)
        : null;
    const pctRecorrido =
      a.origen_lat != null &&
      a.origen_lng != null &&
      a.destino_lat != null &&
      a.destino_lng != null &&
      a.vehiculo_lat != null &&
      a.vehiculo_lng != null
        ? Math.round(
            progresoRuta(
              [a.origen_lat, a.origen_lng],
              [a.destino_lat, a.destino_lng],
              [a.vehiculo_lat, a.vehiculo_lng],
            ) * 100,
          )
        : null;
    // Ruta restante y si REQUIERE tanqueo (la autonomía no la cubre). Este es el
    // control del panel: si no requiere, se deshabilita "Autorizar".
    const rutaRestante =
      a.ruta_km != null && pctRecorrido != null ? a.ruta_km * (1 - pctRecorrido / 100) : null;
    const requiereTanqueo =
      a.autonomia_km != null && rutaRestante != null ? a.autonomia_km < rutaRestante : true;

    return {
      capacidad: a.capacidad_tanque_gal != null ? `${entero(a.capacidad_tanque_gal, 3)} gal` : '—',
      nivel: pctNivel != null ? `${pctNivel}%` : '—',
      // "Última consulta nivel tanque": fecha/hora de la lectura de nivel en Geotab.
      fechaTele: formatFecha(a.vehiculo_nivel_at),
      galonesEst: galonesEst != null ? `${entero(galonesEst, 3)} gal` : '—',
      kmRuta: a.ruta_km != null ? `${entero(a.ruta_km)} km` : '—',
      recorrido: pctRecorrido != null ? `${pctRecorrido}%` : '—',
      autonomia: a.autonomia_km != null ? `${entero(a.autonomia_km, 1)} km` : '—',
      rutaRestante,
      requiereTanqueo,
    };
  });

  protected autorizar(id: string): void {
    this.ejecutar(
      'autorizar',
      this.autorizaciones.autorizar(id).pipe(map(() => 'Tanqueo autorizado')),
    );
  }

  protected preautorizarEds(id: string): void {
    this.ejecutar(
      'eds',
      this.autorizaciones
        .preautorizarEds(id)
        .pipe(map(() => 'Pre-autorización registrada en la EDS')),
    );
  }

  protected notificar(id: string): void {
    this.ejecutar(
      'notificar',
      this.autorizaciones.notificar(id).pipe(
        map((r) => {
          if (!r.whatsapp.enviado) {
            throw new ApiError(
              `WhatsApp no enviado: ${r.whatsapp.motivo ?? '—'}`,
              'whatsapp_no_enviado',
              502,
            );
          }
          return 'Mensaje enviado por Whatsapp';
        }),
      ),
    );
  }

  private ejecutar(nombre: Accion, operacion: Observable<string>): void {
    this.accion.set(nombre);
    this.msg.set(null);
    operacion.subscribe({
      next: (texto) => {
        this.msg.set({ tipo: 'ok', texto });
        this.accion.set(null);
        this.carga.refetch();
      },
      error: (e: unknown) => {
        this.msg.set({ tipo: 'error', texto: mensajeDeError(e, 'Error') });
        this.accion.set(null);
      },
    });
  }
}
