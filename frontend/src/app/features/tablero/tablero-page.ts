import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import type { ColumnDef } from '@tanstack/angular-table';
import type { EquipoFilaApi, MetricasFiltros } from '../../core/api/api-types';
import { MetricasService } from '../../core/servicios/metricas.service';
import { MapaTablero } from '../../shared/mapa/mapa-tablero';
import { Card } from '../../shared/ui/card';
import { DataTable } from '../../shared/ui/data-table';
import { Kpi } from '../../shared/ui/kpi';
import { Select } from '../../shared/ui/select';
import { Skeleton } from '../../shared/ui/skeleton';
import { EmptyState, ErrorState, LoadingState } from '../../shared/ui/states';
import { cargarApi } from '../../shared/utils/cargar-api';
import { formatFecha } from '../../shared/utils/fecha';
import { MESES, cap, cop, entero, plata } from '../../shared/utils/formato';
import { GraficoUtilizacion } from './grafico-utilizacion';

interface Filtros {
  programa: string;
  proveedor: string;
  producto: string;
  anio: string;
  mes: string;
  ruta: string;
}
const VACIO: Filtros = { programa: '', proveedor: '', producto: '', anio: '', mes: '', ruta: '' };
// Por defecto se filtra el mes/año actual: el "precio ideal" usa precios vigentes
// de las EDS, así que la comparación es fiel para tanqueos recientes.
const HOY = new Date();
const INICIAL: Filtros = {
  ...VACIO,
  anio: String(HOY.getFullYear()),
  mes: String(HOY.getMonth() + 1),
};

/** Tablero de Recuperación de Margen: KPIs, utilización por proveedor, mapas e historial. */
@Component({
  selector: 'app-tablero-page',
  imports: [
    Card,
    Select,
    Kpi,
    DataTable,
    Skeleton,
    MapaTablero,
    GraficoUtilizacion,
    LoadingState,
    ErrorState,
    EmptyState,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'block space-y-4' },
  template: `
    <div class="flex flex-wrap items-center gap-3">
      <div>
        <h1 class="text-h1">Recuperación de Margen</h1>
        <p class="text-body text-text-muted">
          Gasto real vs. ideal (estación más barata) y techo (estación más cara) por ruta. El ahorro
          compara lo pagado contra la estación más cara.
        </p>
      </div>
      @if (carga.loading() && carga.data()) {
        <span class="text-caption text-text-muted">Actualizando…</span>
      }
    </div>

    <!-- Filtros -->
    <app-card>
      <div class="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-6">
        <app-select
          label="Año"
          [value]="filtros().anio"
          (valueChange)="fijar('anio', $event)"
          placeholder="Todos"
          [options]="opAnios()"
        />
        <app-select
          label="Mes"
          [value]="filtros().mes"
          (valueChange)="fijar('mes', $event)"
          placeholder="Todos"
          [options]="opMeses()"
        />
        <app-select
          label="Programa"
          [value]="filtros().programa"
          (valueChange)="fijar('programa', $event)"
          placeholder="Todos"
          [options]="opProgramas()"
        />
        <app-select
          label="Proveedor"
          [value]="filtros().proveedor"
          (valueChange)="fijar('proveedor', $event)"
          placeholder="Todos"
          [options]="opProveedores()"
        />
        <app-select
          label="Producto"
          [value]="filtros().producto"
          (valueChange)="fijar('producto', $event)"
          placeholder="Todos"
          [options]="opProductos()"
        />
        <app-select
          label="Ruta"
          [value]="filtros().ruta"
          (valueChange)="fijar('ruta', $event)"
          placeholder="Todas"
          [options]="opRutas()"
        />
      </div>
    </app-card>

    @if (!carga.data() && carga.loading()) {
      <app-card>
        <app-loading-state mensaje="Calculando KPIs…" />
      </app-card>
    } @else if (!carga.data() && carga.error()) {
      <app-card>
        <app-error-state
          [mensaje]="carga.error() ?? ''"
          [conReintento]="true"
          (reintentar)="carga.refetch()"
        />
      </app-card>
    } @else if (carga.data(); as data) {
      @if (data.tanqueos_total === 0) {
        <app-card>
          <app-empty-state
            titulo="Sin tanqueos en el filtro"
            mensaje="Ajusta los filtros o espera a que se registren tanqueos."
          />
        </app-card>
      } @else {
        <!-- KPIs -->
        <div class="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
          <app-kpi
            label="Gasto real (pagado)"
            [valor]="plata(data.gasto_real)"
            [sub]="entero(data.galones_total, 3) + ' gal · ' + data.tanqueos_total + ' tanqueos'"
          />
          <app-kpi
            label="Gasto ideal (estación más barata)"
            [valor]="plata(data.gasto_ideal)"
            [sub]="
              data.precio_minimo_ruta != null
                ? 'Precio mín. ruta: ' + cop(data.precio_minimo_ruta) + '/gal'
                : ''
            "
          />
          <app-kpi label="Gasto máximo (estación más cara)" [valor]="plata(data.gasto_maximo)" />
          <app-kpi
            label="Ahorro vs. estación más cara"
            [valor]="cop(data.ahorro_vs_maximo)"
            [destacado]="true"
            [sub]="'Oportunidad vs. ideal: ' + cop(data.oportunidad_vs_ideal)"
          />
        </div>

        <!-- Utilización por proveedor (ancho completo) -->
        <app-card>
          <h3 class="mb-4 text-h3">Utilización por proveedor</h3>
          @if (data.utilizacion_eds.length === 0) {
            <app-empty-state mensaje="Sin datos." />
          } @else {
            @defer (on immediate) {
              <app-grafico-utilizacion [datos]="data.utilizacion_eds" />
            } @placeholder {
              <app-skeleton class="h-64 w-full" />
            }
          }
        </app-card>

        <!-- Mapas -->
        <div class="grid grid-cols-1 gap-4 lg:grid-cols-2">
          <app-card>
            <h3 class="mb-4 text-h3">Galones consumidos por EDS</h3>
            @defer (on immediate) {
              <app-mapa-tablero modo="galones" [estaciones]="data.galones_por_eds" />
            } @placeholder {
              <app-skeleton class="h-80 w-full" />
            }
          </app-card>
          <app-card>
            <h3 class="mb-4 text-h3">Rutas</h3>
            @defer (on immediate) {
              <app-mapa-tablero modo="rutas" [rutas]="data.rutas" />
            } @placeholder {
              <app-skeleton class="h-80 w-full" />
            }
          </app-card>
        </div>

        <!-- Historial de tanqueos -->
        <app-card class="p-0">
          <h3 class="px-6 pt-6 text-h3">Historial de tanqueos</h3>
          <div class="mt-4">
            <app-data-table
              [columns]="columnasEquipo"
              [data]="data.equipos"
              emptyMessage="Sin tanqueos en el filtro."
            />
          </div>
        </app-card>
      }
    }
  `,
})
export class TableroPage {
  private readonly metricas = inject(MetricasService);

  protected readonly filtros = signal<Filtros>(INICIAL);

  protected readonly carga = cargarApi(() => {
    const f = this.filtros();
    const filtros: MetricasFiltros = { ...f };
    return this.metricas.recuperacion(filtros);
  });

  protected readonly plata = plata;
  protected readonly cop = cop;
  protected readonly entero = entero;

  // Opciones de los selects: las provee el servidor y se conservan al recargar.
  private readonly opciones = computed(() => this.carga.data()?.opciones);
  protected readonly opAnios = computed(() =>
    (this.opciones()?.anios ?? []).map((a) => ({ value: String(a), label: String(a) })),
  );
  protected readonly opMeses = computed(() =>
    (this.opciones()?.meses ?? []).map((m) => ({ value: String(m), label: MESES[m] ?? String(m) })),
  );
  protected readonly opProgramas = computed(() =>
    (this.opciones()?.programas ?? []).map((p) => ({ value: p, label: p })),
  );
  protected readonly opProveedores = computed(() =>
    (this.opciones()?.proveedores ?? []).map((p) => ({ value: p, label: cap(p) })),
  );
  protected readonly opProductos = computed(() =>
    (this.opciones()?.productos ?? []).map((p) => ({ value: p, label: cap(p) })),
  );
  protected readonly opRutas = computed(() =>
    (this.opciones()?.rutas ?? []).map((r) => ({ value: r, label: r })),
  );

  protected readonly columnasEquipo: ColumnDef<EquipoFilaApi, unknown>[] = [
    {
      accessorKey: 'fecha',
      header: 'Fecha tanqueo',
      cell: ({ row }) => formatFecha(row.original.fecha),
    },
    { accessorKey: 'equipo', header: 'Placa' },
    {
      accessorKey: 'mes',
      header: 'Mes',
      cell: ({ row }) => MESES[row.original.mes] ?? row.original.mes,
    },
    {
      accessorKey: 'galones',
      header: 'Galones',
      cell: ({ row }) => entero(row.original.galones, 1),
    },
    {
      accessorKey: 'valor_galon',
      header: 'Valor por galón',
      cell: ({ row }) => cop(row.original.valor_galon),
    },
    {
      accessorKey: 'valor_total',
      header: 'Valor total',
      cell: ({ row }) => cop(row.original.valor_total),
    },
    { accessorKey: 'eds', header: 'EDS' },
    { accessorKey: 'ciudad', header: 'Ciudad', cell: ({ row }) => row.original.ciudad ?? '—' },
  ];

  protected fijar(clave: keyof Filtros, valor: string): void {
    this.filtros.update((f) => ({ ...f, [clave]: valor }));
  }
}
