import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import type { ColumnDef } from '@tanstack/angular-table';
import { debounceTime, map } from 'rxjs';
import type { ViajeApi } from '../../core/api/api-types';
import { ViajesService } from '../../core/servicios/viajes.service';
import { Badge } from '../../shared/ui/badge';
import { Button } from '../../shared/ui/button';
import { Card } from '../../shared/ui/card';
import { CeldaDef, DataTable } from '../../shared/ui/data-table';
import { Input } from '../../shared/ui/input';
import { Select } from '../../shared/ui/select';
import { EmptyState, ErrorState, LoadingState } from '../../shared/ui/states';
import { cargarApi } from '../../shared/utils/cargar-api';
import { DESPLAZAMIENTO_OPCIONES } from '../../shared/utils/desplazamiento';
import { formatFecha } from '../../shared/utils/fecha';
import { MESES_OPCIONES, entero } from '../../shared/utils/formato';
import {
  ESTADO_VIAJE_BADGE,
  ESTADO_VIAJE_OPCIONES,
  SI_NO_OPCIONES,
  autonomiaDe,
  fechaInicioViaje,
  galonesEstimados,
  necesitaTanqueo,
  pctNivelTanque,
  pctRecorridoMostrable,
} from '../../shared/utils/viaje';
import { AccionesViaje } from './acciones-viaje';

const HOY = new Date();

/** Texto de la ruta: NOMBRERUTA si viene con texto; si no, CODIGORUTA (igual que en la sincronización). */
const textoRuta = (v: ViajeApi) => v.nombre_ruta || v.codigo_ruta || v.ruta || '—';

/** Panel de Despacho: viajes activos con autonomía, nivel de tanque y acciones de tanqueo. */
@Component({
  selector: 'app-despacho-page',
  imports: [
    Card,
    Badge,
    Button,
    Input,
    Select,
    DataTable,
    CeldaDef,
    AccionesViaje,
    LoadingState,
    ErrorState,
    EmptyState,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'block space-y-4' },
  template: `
    <div class="flex items-center gap-3">
      <div>
        <h1 class="text-h1">Panel de Despacho</h1>
        <p class="text-body text-text-muted">
          Programa el viaje y dispara el cálculo de autonomía y la selección de estación.
        </p>
      </div>
      <!-- Refresco en segundo plano: la tabla sigue montada para conservar el orden. -->
      @if (carga.loading() && carga.data()) {
        <span class="text-caption text-text-muted">Actualizando…</span>
      }
    </div>

    <app-card>
      <div class="flex flex-wrap items-end gap-3">
        <app-select
          label="Año (inicio viaje)"
          [(value)]="anio"
          placeholder="Todos"
          [options]="anios()"
          selectClass="w-36"
        />
        <app-select
          label="Mes (inicio viaje)"
          [(value)]="mes"
          placeholder="Todos"
          [options]="meses"
          selectClass="w-40"
        />
        <app-input label="Placa" [(value)]="placa" placeholder="Ej. SNU123" inputClass="w-40" />
        <app-input
          label="Manifiesto"
          [(value)]="manifiesto"
          placeholder="Ej. 301457403"
          inputClass="w-44"
        />
        <app-select
          label="Ruta"
          [(value)]="ruta"
          placeholder="Todas"
          [options]="rutas()"
          selectClass="w-56"
        />
        <app-select
          label="Estado"
          [(value)]="estado"
          placeholder="Todos"
          [options]="estadoOpciones"
          selectClass="w-44"
        />
        <app-select
          label="Desplazamiento"
          [(value)]="desplazamiento"
          placeholder="Todos"
          [options]="desplazamientoOpciones"
          selectClass="w-44"
        />
        <app-select
          label="Telemetría de tanque actualizada"
          [(value)]="telemetria"
          placeholder="Todos"
          [options]="siNo"
          selectClass="w-56"
        />
        <app-select
          label="Necesita tanqueo"
          [(value)]="necesita"
          placeholder="Todos"
          [options]="siNo"
          selectClass="w-44"
        />
        <button appButton type="button" variant="secondary" (click)="limpiar()">Limpiar</button>
      </div>
    </app-card>

    <app-card class="p-0">
      @if (!carga.data() && carga.loading()) {
        <app-loading-state mensaje="Cargando viajes…" />
      } @else if (!carga.data() && carga.error()) {
        <app-error-state
          [mensaje]="carga.error() ?? ''"
          [conReintento]="true"
          (reintentar)="carga.refetch()"
        />
      } @else if (carga.data() && filtrados().length === 0) {
        <app-empty-state
          titulo="Sin viajes"
          mensaje="No hay viajes para los filtros seleccionados."
        />
      } @else if (carga.data()) {
        <app-data-table [columns]="columnas" [data]="filtrados()" emptyMessage="Sin viajes.">
          <ng-template appCelda="estado" [appCeldaDe]="filtrados()" let-viaje>
            <app-badge [variant]="estadoBadge[viaje.estado]">{{ viaje.estado }}</app-badge>
          </ng-template>
          <ng-template appCelda="acciones" [appCeldaDe]="filtrados()" let-viaje>
            <app-acciones-viaje [viaje]="viaje" (cambio)="carga.refetch()" />
          </ng-template>
        </app-data-table>
      }
    </app-card>
  `,
})
export class DespachoPage {
  private readonly viajes = inject(ViajesService);

  // Filtros — se envían al servidor (query params); la BD solo devuelve lo visible.
  protected readonly placa = signal('');
  protected readonly manifiesto = signal('');
  protected readonly ruta = signal('');
  protected readonly estado = signal('en_ruta'); // por defecto: viajes en ruta
  // Año y mes de la fecha de inicio de viaje: por defecto, el mes/año actual.
  protected readonly anio = signal(String(HOY.getFullYear()));
  protected readonly mes = signal(String(HOY.getMonth() + 1));
  // Tipo de desplazamiento (cargado/vacío): por defecto, cargados.
  protected readonly desplazamiento = signal('cargado');
  // Telemetría de tanque actualizada (última hora). Se deja en "Todos" por defecto:
  // apilarlo con "Necesita tanqueo = Sí" y depender del scheduler dejaba la lista
  // vacía. Sigue disponible para filtrar frescura cuando se necesite.
  protected readonly telemetria = signal('');
  // Necesita tanqueo (ruta restante > autonomía): por defecto solo los que sí.
  protected readonly necesita = signal('si');

  // Placa y manifiesto se escriben letra a letra: se difieren (debounce) para no
  // consultar en cada tecla.
  private readonly placaQ = toSignal(
    toObservable(this.placa).pipe(
      debounceTime(300),
      map((s) => s.trim()),
    ),
    { initialValue: '' },
  );
  private readonly manifiestoQ = toSignal(
    toObservable(this.manifiesto).pipe(
      debounceTime(300),
      map((s) => s.trim()),
    ),
    { initialValue: '' },
  );

  protected readonly carga = cargarApi(() =>
    this.viajes.listar({
      estado: this.estado(),
      ruta: this.ruta(),
      placa: this.placaQ(),
      manifiesto: this.manifiestoQ(),
      anio: this.anio(),
      mes: this.mes(),
      desplazamiento: this.desplazamiento(),
      telemetria: this.telemetria(),
    }),
  );

  protected readonly meses = MESES_OPCIONES;
  protected readonly estadoOpciones = ESTADO_VIAJE_OPCIONES;
  protected readonly desplazamientoOpciones = DESPLAZAMIENTO_OPCIONES;
  protected readonly siNo = SI_NO_OPCIONES;
  protected readonly estadoBadge = ESTADO_VIAJE_BADGE;

  // Opciones de los dropdowns: las provee el servidor (sobre los viajes activos),
  // así son estables aunque el resultado esté filtrado.
  protected readonly rutas = computed(() =>
    (this.carga.data()?.opciones.rutas ?? []).map((c) => ({ value: c, label: c })),
  );
  protected readonly anios = computed(() =>
    (this.carga.data()?.opciones.anios ?? [HOY.getFullYear()]).map((a) => ({
      value: String(a),
      label: String(a),
    })),
  );

  // El servidor ya aplicó los filtros de BD; "Necesita tanqueo" se resuelve en
  // cliente (usa el % recorrido, que no es columna). Luego se ordena por inicio asc.
  protected readonly filtrados = computed(() => {
    const necesita = this.necesita();
    const inicio = (v: ViajeApi) => fechaInicioViaje(v)?.getTime() ?? Infinity;
    return (this.carga.data()?.viajes ?? [])
      .filter((v) => !necesita || necesitaTanqueo(v) === (necesita === 'si'))
      .slice()
      .sort((a, b) => inicio(a) - inicio(b));
  });

  protected readonly columnas: ColumnDef<ViajeApi, unknown>[] = [
    { accessorKey: 'placa', header: 'Placa' },
    {
      accessorKey: 'manifiesto',
      header: 'Manifiesto',
      cell: ({ row }) => row.original.manifiesto ?? '—',
    },
    { accessorKey: 'ruta', header: 'Ruta', cell: ({ row }) => textoRuta(row.original) },
    { accessorKey: 'ruta_km', header: 'Km ruta', cell: ({ row }) => entero(row.original.ruta_km) },
    {
      accessorKey: 'inicio_at',
      header: 'Inicio viaje',
      // En programado aún no hay inicio real; se muestra la fecha de despacho
      // programada (FO) como fecha prevista de inicio.
      cell: ({ row }) => formatFecha(row.original.inicio_at ?? row.original.fecha_despacho),
    },
    { accessorKey: 'estado', header: 'Estado' },
    {
      accessorKey: 'capacidad_tanque_gal',
      header: 'Capacidad (gal)',
      cell: ({ row }) => entero(row.original.capacidad_tanque_gal, 3),
    },
    {
      id: 'nivel_telemetria',
      header: 'Nivel tanque',
      // Nivel de la telemetría como % del tanque.
      cell: ({ row }) => {
        const pct = pctNivelTanque(row.original);
        return pct != null ? `${pct}%` : '—';
      },
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
    {
      accessorKey: 'autonomia_km',
      header: 'Autonomía (km)',
      // Autonomía actual del vehículo; si falta telemetría, la estimada del viaje.
      cell: ({ row }) => entero(autonomiaDe(row.original), 1),
    },
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

  protected limpiar(): void {
    this.placa.set('');
    this.manifiesto.set('');
    this.ruta.set('');
    this.estado.set('');
    this.anio.set('');
    this.mes.set('');
    this.desplazamiento.set('');
    this.telemetria.set('');
    this.necesita.set('');
  }
}
