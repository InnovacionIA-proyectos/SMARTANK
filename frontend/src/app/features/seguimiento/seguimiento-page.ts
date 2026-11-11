import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import type { ColumnDef } from '@tanstack/angular-table';
import type { ViajeApi } from '../../core/api/api-types';
import { ViajesService } from '../../core/servicios/viajes.service';
import { Badge } from '../../shared/ui/badge';
import { Button } from '../../shared/ui/button';
import { Card } from '../../shared/ui/card';
import { CeldaDef, DataTable } from '../../shared/ui/data-table';
import { Kpi } from '../../shared/ui/kpi';
import { Select } from '../../shared/ui/select';
import { EmptyState, ErrorState, LoadingState } from '../../shared/ui/states';
import { cargarApi } from '../../shared/utils/cargar-api';
import { DESPLAZAMIENTO_OPCIONES } from '../../shared/utils/desplazamiento';
import { formatFecha } from '../../shared/utils/fecha';
import { entero } from '../../shared/utils/formato';
import {
  ESTADO_VIAJE_BADGE,
  ESTADO_VIAJE_OPCIONES,
  SI_NO_OPCIONES,
  autonomiaDe,
  galonesEstimados,
  necesitaTanqueo,
  pctRecorridoMostrable,
  semaforoAutonomia,
} from '../../shared/utils/viaje';
import { PanelRuta } from './panel-ruta';

/** Seguimiento Control Combustible: viajes con recomendación activa, KPIs y semáforo de autonomía. */
@Component({
  selector: 'app-seguimiento-page',
  imports: [
    Card,
    Badge,
    Button,
    Select,
    DataTable,
    CeldaDef,
    Kpi,
    PanelRuta,
    LoadingState,
    ErrorState,
    EmptyState,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'block space-y-4' },
  template: `
    <div class="flex flex-wrap items-center gap-3">
      <div>
        <h1 class="text-h1">Seguimiento Control Combustible</h1>
        <p class="text-body text-text-muted">
          Monitoreo de los viajes con recomendación de tanqueo activa: nivel de tanque y kilómetros
          disponibles por viaje, con semáforo de riesgo.
        </p>
      </div>
      @if (carga.loading() && carga.data()) {
        <span class="text-caption text-text-muted">Actualizando…</span>
      }
    </div>

    <!-- KPIs -->
    <div class="grid grid-cols-2 gap-4 lg:grid-cols-4">
      <app-kpi label="Vehículos en seguimiento" [valor]="'' + kpis().total" />
      <app-kpi label="Crítico (< 100 km)" [valor]="'' + kpis().critico" variant="danger" />
      <app-kpi label="Precaución (100–200 km)" [valor]="'' + kpis().precaucion" variant="warning" />
      <app-kpi label="Autonomía promedio" [valor]="promedioTexto()" />
    </div>

    <!-- Filtros + leyenda -->
    <app-card>
      <div class="flex flex-wrap items-end gap-3">
        <app-select
          label="Estado"
          [(value)]="estado"
          placeholder="Todos"
          [options]="estadoOpciones"
          selectClass="w-44"
        />
        <app-select
          label="Ruta"
          [(value)]="ruta"
          placeholder="Todas"
          [options]="rutas()"
          selectClass="w-56"
        />
        <app-select
          label="Desplazamiento"
          [(value)]="desplazamiento"
          placeholder="Todos"
          [options]="desplazamientoOpciones"
          selectClass="w-44"
        />
        <app-select
          label="Necesita tanqueo"
          [(value)]="necesita"
          placeholder="Todos"
          [options]="siNo"
          selectClass="w-44"
        />
        <div class="ml-auto flex items-center gap-3 text-caption text-text-muted">
          <span class="flex items-center gap-1.5">
            <span class="inline-block h-3 w-3 rounded-full bg-danger"></span> &lt; 100 km
          </span>
          <span class="flex items-center gap-1.5">
            <span class="inline-block h-3 w-3 rounded-full bg-warning"></span> 100–200 km
          </span>
          <span class="flex items-center gap-1.5">
            <span class="inline-block h-3 w-3 rounded-full bg-success"></span> &gt; 200 km
          </span>
        </div>
      </div>
    </app-card>

    @if (seleccion(); as sel) {
      <app-panel-ruta [viajeId]="sel.id" [placa]="sel.placa" (cerrar)="seleccion.set(null)" />
    }

    <app-card class="p-0">
      @if (!carga.data() && carga.loading()) {
        <app-loading-state mensaje="Cargando flota…" />
      } @else if (!carga.data() && carga.error()) {
        <app-error-state
          [mensaje]="carga.error() ?? ''"
          [conReintento]="true"
          (reintentar)="carga.refetch()"
        />
      } @else if (carga.data() && filtrados().length === 0) {
        <app-empty-state
          titulo="Sin viajes con recomendación activa"
          mensaje="No hay viajes con una recomendación de tanqueo vigente para los filtros seleccionados."
        />
      } @else if (carga.data()) {
        <app-data-table
          [columns]="columnas"
          [data]="filtrados()"
          [rowFlagged]="esCritico"
          emptyMessage="Sin vehículos."
        >
          <ng-template appCelda="estado" [appCeldaDe]="filtrados()" let-viaje>
            <app-badge [variant]="estadoBadge[viaje.estado]">{{
              estadoTexto(viaje.estado)
            }}</app-badge>
          </ng-template>
          <ng-template appCelda="autonomia" [appCeldaDe]="filtrados()" let-viaje>
            <app-badge [variant]="semaforo(viaje).variant">{{ semaforo(viaje).texto }}</app-badge>
          </ng-template>
          <ng-template appCelda="acciones" [appCeldaDe]="filtrados()" let-viaje>
            <button appButton variant="secondary" type="button" (click)="verRuta(viaje)">
              Ver ruta
            </button>
          </ng-template>
        </app-data-table>
      }
    </app-card>
  `,
})
export class SeguimientoPage {
  private readonly viajes = inject(ViajesService);

  protected readonly estado = signal(''); // todos los estados con recomendación activa
  protected readonly ruta = signal('');
  protected readonly desplazamiento = signal('cargado'); // por defecto: cargados
  protected readonly necesita = signal(''); // necesita tanqueo: '' todos, 'si', 'no'
  protected readonly seleccion = signal<{ id: string; placa: string } | null>(null);

  // El seguimiento monitorea solo los viajes con una recomendación de tanqueo
  // vigente; el filtro (y estado/ruta/desplazamiento) se aplica en el servidor.
  protected readonly carga = cargarApi(() =>
    this.viajes.listar({
      con_recomendacion: '1',
      estado: this.estado(),
      ruta: this.ruta(),
      desplazamiento: this.desplazamiento(),
    }),
  );

  protected readonly estadoOpciones = ESTADO_VIAJE_OPCIONES;
  protected readonly desplazamientoOpciones = DESPLAZAMIENTO_OPCIONES;
  protected readonly siNo = SI_NO_OPCIONES;
  protected readonly estadoBadge = ESTADO_VIAJE_BADGE;

  private readonly viajesCargados = computed(() => this.carga.data()?.viajes ?? []);
  protected readonly rutas = computed(() =>
    (this.carga.data()?.opciones.rutas ?? []).map((c) => ({ value: c, label: c })),
  );

  // El servidor ya filtró en BD; "Necesita tanqueo" se resuelve en cliente (usa el
  // % recorrido). Luego se ordena por autonomía ascendente (crítico primero).
  protected readonly filtrados = computed(() => {
    const necesita = this.necesita();
    return this.viajesCargados()
      .filter((v) => !necesita || necesitaTanqueo(v) === (necesita === 'si'))
      .slice()
      .sort((a, b) => (autonomiaDe(a) ?? Infinity) - (autonomiaDe(b) ?? Infinity));
  });

  protected readonly kpis = computed(() => {
    const viajes = this.viajesCargados();
    const km = viajes.map(autonomiaDe).filter((k): k is number => k != null);
    const critico = km.filter((k) => k < 100).length;
    const precaucion = km.filter((k) => k >= 100 && k <= 200).length;
    const prom = km.length ? Math.round(km.reduce((s, k) => s + k, 0) / km.length) : 0;
    return { total: viajes.length, critico, precaucion, prom };
  });
  protected readonly promedioTexto = computed(() => `${entero(this.kpis().prom, 3)} km`);

  protected readonly columnas: ColumnDef<ViajeApi, unknown>[] = [
    { accessorKey: 'placa', header: 'Placa' },
    {
      accessorKey: 'manifiesto',
      header: 'Manifiesto',
      cell: ({ row }) => row.original.manifiesto ?? '—',
    },
    {
      accessorKey: 'ruta',
      header: 'Ruta',
      // Igual que en despacho: NOMBRERUTA si viene con texto; si no, CODIGORUTA.
      cell: ({ row }) =>
        row.original.nombre_ruta || row.original.codigo_ruta || row.original.ruta || '—',
    },
    { accessorKey: 'ruta_km', header: 'Km ruta', cell: ({ row }) => entero(row.original.ruta_km) },
    {
      accessorKey: 'inicio_at',
      header: 'Inicio viaje',
      // En programado aún no hay inicio real; se muestra la fecha de despacho (FO).
      cell: ({ row }) => formatFecha(row.original.inicio_at ?? row.original.fecha_despacho),
    },
    { accessorKey: 'estado', header: 'Estado' },
    {
      accessorKey: 'capacidad_tanque_gal',
      header: 'Capacidad (gal)',
      cell: ({ row }) => entero(row.original.capacidad_tanque_gal, 3),
    },
    {
      accessorKey: 'nivel_tanque_actual',
      header: 'Nivel (gal)',
      cell: ({ row }) => entero(row.original.nivel_tanque_actual, 3),
    },
    {
      accessorKey: 'nivel_at',
      header: 'Última consulta nivel tanque',
      // Fecha/hora de la lectura de NIVEL que devuelve Geotab (no la del sync).
      cell: ({ row }) => formatFecha(row.original.nivel_at),
    },
    {
      id: 'galones_estimados',
      header: 'Galones estimados',
      // capacidad × nivel(%) de la telemetría.
      cell: ({ row }) => entero(galonesEstimados(row.original), 3),
    },
    { id: 'autonomia', header: 'Autonomía (km)' },
    {
      id: 'pct_recorrido',
      header: '% recorrido',
      // Avance del vehículo sobre la ruta (proyección origen→destino).
      cell: ({ row }) => {
        const pct = pctRecorridoMostrable(row.original);
        return pct != null ? `${pct}%` : '—';
      },
    },
    { id: 'acciones', header: '' },
  ];

  /** Filas críticas (< 100 km) con fondo danger sutil. */
  protected readonly esCritico = (v: ViajeApi): boolean => {
    const km = autonomiaDe(v);
    return km != null && km < 100;
  };

  protected semaforo(v: ViajeApi) {
    return semaforoAutonomia(autonomiaDe(v));
  }

  protected estadoTexto(estado: string): string {
    return estado.replace(/_/g, ' ');
  }

  protected verRuta(v: ViajeApi): void {
    this.seleccion.set({ id: v.id, placa: v.placa });
  }
}
